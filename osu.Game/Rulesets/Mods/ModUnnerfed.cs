// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// A local-only "demo" mod. While active, mods which make gameplay easier (Easy, No Fail, Half Time, Relax, Autopilot, Spun Out,
    /// and Difficulty Adjust settings lowered below the beatmap's own values) no longer reduce star rating, performance points or score.
    /// Mods which make gameplay harder keep their usual effect.
    /// Scores set with this mod are never submitted online. They are kept as local, unranked scores and replays.
    /// </summary>
    public abstract class ModUnnerfed : Mod, IApplicableMod
    {
        public override string Name => "Unnerfed";
        public override string Acronym => "UN";
        public override LocalisableString Description => "Local demo mode: easier mods no longer lower star rating, pp or score. Scores are never submitted.";
        public override IconUsage? Icon => FontAwesome.Solid.Flask;
        public override ModType Type => ModType.Fun;
        public override bool Ranked => false;
        public override bool UserPlayable => true;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;
        public override bool ValidForFreestyleAsRequiredMod => false;
        public override Type[] IncompatibleMods => new[] { typeof(ModAutoplay) };

        /// <summary>
        /// Whether the given <paramref name="mod"/> is one that makes gameplay easier, and is therefore ignored for calculations while <see cref="ModUnnerfed"/> is active.
        /// </summary>
        public static bool IsEasierMod(Mod mod)
        {
            switch (mod.Type)
            {
                case ModType.DifficultyReduction:
                    return true;

                case ModType.Automation:
                    // Relax, Autopilot and Spun Out still require the player to play. Autoplay / Cinema never produce real scores.
                    return mod is not ModAutoplay;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the mods that star rating, performance and score multiplier calculations should use.
        /// If <paramref name="mods"/> does not contain <see cref="ModUnnerfed"/>, the input is returned unchanged.
        /// </summary>
        /// <param name="mods">The mods selected for gameplay.</param>
        /// <param name="originalDifficulty">
        /// The beatmap's difficulty settings before any mods are applied.
        /// Used to detect <see cref="ModDifficultyAdjust"/> settings which were lowered. If <see langword="null"/>, Difficulty Adjust is left untouched.
        /// </param>
        public static IEnumerable<Mod> GetEffectiveMods(IEnumerable<Mod> mods, IBeatmapDifficultyInfo? originalDifficulty)
        {
            var modList = mods as IReadOnlyCollection<Mod> ?? mods.ToArray();

            if (!modList.Any(m => m is ModUnnerfed))
                return modList;

            var result = new List<Mod>();

            foreach (var mod in modList)
            {
                if (mod is ModUnnerfed || IsEasierMod(mod))
                    continue;

                if (mod is ModDifficultyAdjust difficultyAdjust && originalDifficulty != null)
                {
                    result.Add(removeLoweredSettings(difficultyAdjust, originalDifficulty));
                    continue;
                }

                result.Add(mod);
            }

            return result;
        }

        /// <summary>
        /// Returns a copy of <paramref name="score"/> whose mods are replaced with the result of <see cref="GetEffectiveMods"/>.
        /// If the score does not use <see cref="ModUnnerfed"/>, the same instance is returned.
        /// </summary>
        public static ScoreInfo GetEffectiveScore(ScoreInfo score)
        {
            var mods = score.Mods;

            if (!mods.Any(m => m is ModUnnerfed))
                return score;

            var clone = score.DeepClone();
            clone.Mods = GetEffectiveMods(mods, score.BeatmapInfo?.Difficulty).ToArray();
            return clone;
        }

        /// <summary>
        /// Any Difficulty Adjust setting which was set below the beatmap's own value (making the beatmap easier) is reset to the beatmap's value.
        /// Settings raised above the beatmap's value are kept.
        /// </summary>
        private static Mod removeLoweredSettings(ModDifficultyAdjust difficultyAdjust, IBeatmapDifficultyInfo originalDifficulty)
        {
            var clone = (ModDifficultyAdjust)difficultyAdjust.DeepClone();

            foreach (var (_, property) in clone.GetOrderedSettingsSourceProperties())
            {
                if (property.GetValue(clone) is not DifficultyBindable bindable)
                    continue;

                if (bindable.Value == null || bindable.ReadCurrentFromDifficulty == null)
                    continue;

                if (bindable.Value.Value < bindable.ReadCurrentFromDifficulty(originalDifficulty))
                    bindable.Value = null;
            }

            return clone;
        }
    }
}
