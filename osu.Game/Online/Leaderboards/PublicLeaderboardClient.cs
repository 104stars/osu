// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;

namespace osu.Game.Online.Leaderboards
{
    /// <summary>
    /// Reads public beatmap leaderboards from the official osu! API using a user-owned OAuth application ("client credentials" grant).
    /// This never logs in as a user and can only read public data. It cannot submit scores or perform any action on an account.
    /// </summary>
    public class PublicLeaderboardClient
    {
        public const string API_ROOT = @"https://osu.ppy.sh";

        /// <summary>
        /// The number of scores requested per leaderboard (the maximum the API allows).
        /// </summary>
        public const int SCORES_PER_REQUEST = 100;

        /// <summary>
        /// Minimum time between two requests, to stay well within the API's rate limits.
        /// </summary>
        private static readonly TimeSpan min_request_interval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// How long a fetched leaderboard is reused before being fetched again.
        /// </summary>
        private static readonly TimeSpan cache_lifetime = TimeSpan.FromMinutes(2);

        private readonly Bindable<string> clientId;
        private readonly Bindable<string> clientSecret;

        private readonly SemaphoreSlim requestLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<(int beatmapId, string ruleset), (DateTimeOffset fetched, APIScoresCollection scores)> cache = new Dictionary<(int, string), (DateTimeOffset, APIScoresCollection)>();

        private string? accessToken;
        private DateTimeOffset accessTokenExpiry;
        private DateTimeOffset lastRequest;

        public PublicLeaderboardClient(OsuConfigManager config)
        {
            clientId = config.GetBindable<string>(OsuSetting.PublicLeaderboardClientID);
            clientSecret = config.GetBindable<string>(OsuSetting.PublicLeaderboardClientSecret);

            // Credentials changed, so any token from the previous credentials is no longer relevant.
            clientId.BindValueChanged(_ => accessToken = null);
            clientSecret.BindValueChanged(_ => accessToken = null);
        }

        /// <summary>
        /// Whether the user has provided OAuth application credentials.
        /// </summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(clientId.Value) && !string.IsNullOrWhiteSpace(clientSecret.Value);

        /// <summary>
        /// Fetches the global top scores of a beatmap.
        /// </summary>
        public Task<APIScoresCollection> GetScoresAsync(IBeatmapInfo beatmap, IRulesetInfo ruleset, CancellationToken cancellationToken)
        {
            int beatmapId = beatmap.OnlineID;
            string rulesetName = ruleset.ShortName;

            return Task.Run(async () =>
            {
                await requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    if (cache.TryGetValue((beatmapId, rulesetName), out var cached) && DateTimeOffset.Now - cached.fetched < cache_lifetime)
                        return cached.scores;

                    cancellationToken.ThrowIfCancellationRequested();

                    string token = await getAccessToken(cancellationToken).ConfigureAwait(false);

                    var request = new OsuJsonWebRequest<APIScoresCollection>($@"{API_ROOT}/api/v2/beatmaps/{beatmapId}/scores");
                    request.AddParameter(@"mode", rulesetName);
                    request.AddParameter(@"limit", SCORES_PER_REQUEST.ToString(CultureInfo.InvariantCulture));
                    request.AddHeader(@"Authorization", $@"Bearer {token}");
                    request.AddHeader(@"x-api-version", apiVersion.ToString(CultureInfo.InvariantCulture));

                    try
                    {
                        await performThrottled(request, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // the token may have been revoked or expired early. fetch a fresh one next time.
                        accessToken = null;
                        throw;
                    }

                    var result = request.ResponseObject ?? throw new InvalidOperationException(@"Leaderboard response was empty.");
                    result.Scores ??= new List<SoloScoreInfo>();

                    cache[(beatmapId, rulesetName)] = (DateTimeOffset.Now, result);
                    return result;
                }
                finally
                {
                    requestLock.Release();
                }
            }, cancellationToken);
        }

        private async Task<string> getAccessToken(CancellationToken cancellationToken)
        {
            if (accessToken != null && DateTimeOffset.Now < accessTokenExpiry)
                return accessToken;

            var request = new OsuJsonWebRequest<ClientCredentialsToken>($@"{API_ROOT}/oauth/token")
            {
                Method = HttpMethod.Post,
            };

            request.AddParameter(@"grant_type", @"client_credentials");
            request.AddParameter(@"client_id", clientId.Value.Trim());
            request.AddParameter(@"client_secret", clientSecret.Value.Trim());
            // "public" only allows reading public data.
            request.AddParameter(@"scope", @"public");

            await performThrottled(request, cancellationToken).ConfigureAwait(false);

            var token = request.ResponseObject;

            if (string.IsNullOrEmpty(token?.AccessToken))
                throw new InvalidOperationException(@"Could not retrieve an access token. Check the client ID and secret in the online settings.");

            accessToken = token.AccessToken;
            // refresh a minute early to avoid using a token right as it expires.
            accessTokenExpiry = DateTimeOffset.Now.AddSeconds(Math.Max(0, token.ExpiresIn - 60));

            Logger.Log(@"Retrieved a read-only token for public leaderboards.", LoggingTarget.Network);
            return accessToken;
        }

        private async Task performThrottled(WebRequest request, CancellationToken cancellationToken)
        {
            var wait = lastRequest + min_request_interval - DateTimeOffset.Now;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);

            lastRequest = DateTimeOffset.Now;
            await request.PerformAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// The API response format version. Matches the format the game itself requests, see <see cref="APIAccess"/>.
        /// </summary>
        private static int apiVersion
        {
            get
            {
                var now = DateTimeOffset.Now;
                return now.Year * 10000 + now.Month * 100 + now.Day;
            }
        }

        private class ClientCredentialsToken
        {
            [JsonProperty(@"access_token")]
            public string? AccessToken { get; set; }

            [JsonProperty(@"expires_in")]
            public long ExpiresIn { get; set; }
        }
    }
}
