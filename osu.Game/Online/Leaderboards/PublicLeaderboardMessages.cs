// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online.Leaderboards
{
    /// <summary>
    /// User-facing messages for reading leaderboards without logging in.
    /// </summary>
    public static class PublicLeaderboardMessages
    {
        public const string NOT_CONFIGURED = @"To see global leaderboards, add your OAuth client ID and secret in Settings > Online > Public leaderboards. Click here to open it.";

        public const string ONLY_GLOBAL = @"Only the Global leaderboard is available without logging in. Switch the leaderboard to Global.";
    }
}
