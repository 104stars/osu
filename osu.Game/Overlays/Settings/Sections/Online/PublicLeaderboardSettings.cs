// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    /// <summary>
    /// Credentials for reading public leaderboards from the official osu! API without logging in.
    /// </summary>
    public partial class PublicLeaderboardSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Public leaderboards (read-only)";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormTextBox
                {
                    Caption = @"OAuth client ID",
                    HintText = @"Create an OAuth application in your osu! account settings (on the website) and paste its client ID here. "
                               + @"It is only used to read global beatmap leaderboards. This game never logs in to your account.",
                    Current = config.GetBindable<string>(OsuSetting.PublicLeaderboardClientID),
                }),
                new SettingsItemV2(new FormPasswordTextBox
                {
                    Caption = @"OAuth client secret",
                    HintText = @"Stored only on this computer. Leave both fields empty to disable public leaderboards.",
                    Current = config.GetBindable<string>(OsuSetting.PublicLeaderboardClientSecret),
                }),
            };
        }
    }
}
