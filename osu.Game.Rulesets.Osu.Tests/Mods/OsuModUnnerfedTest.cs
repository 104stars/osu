// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class OsuModUnnerfedTest : DifficultyCalculatorTest
    {
        private const string beatmap_name = "diffcalc-test";

        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();

        [Test]
        public void TestModIsAvailable()
        {
            Assert.That(new OsuRuleset().CreateModFromAcronym("UN"), Is.InstanceOf<OsuModUnnerfed>());
            Assert.That(new OsuModUnnerfed().Ranked, Is.False);
        }

        [Test]
        public void TestEasierModsDoNotLowerStarRating()
        {
            double noMod = starRating();

            Assert.That(starRating(new OsuModEasy(), new OsuModRelax()), Is.LessThan(noMod), "sanity check: easier mods normally lower star rating");
            Assert.That(starRating(new OsuModEasy(), new OsuModRelax(), new OsuModNoFail(), new OsuModUnnerfed()), Is.EqualTo(noMod).Within(CHECK_PRECISION));
            Assert.That(starRating(new OsuModHalfTime(), new OsuModUnnerfed()), Is.EqualTo(noMod).Within(CHECK_PRECISION));
            Assert.That(starRating(new OsuModAutopilot(), new OsuModSpunOut(), new OsuModUnnerfed()), Is.EqualTo(noMod).Within(CHECK_PRECISION));
        }

        [Test]
        public void TestHarderModsStillRaiseStarRating()
        {
            double hiddenDoubleTime = starRating(new OsuModHidden(), new OsuModDoubleTime());

            Assert.That(hiddenDoubleTime, Is.GreaterThan(starRating()));
            Assert.That(starRating(new OsuModHidden(), new OsuModDoubleTime(), new OsuModRelax(), new OsuModNoFail(), new OsuModUnnerfed()),
                Is.EqualTo(hiddenDoubleTime).Within(CHECK_PRECISION));
        }

        [Test]
        public void TestLoweredDifficultyAdjustSettingsAreIgnored()
        {
            var beatmap = GetBeatmap(beatmap_name);
            float originalCircleSize = beatmap.BeatmapInfo.Difficulty.CircleSize;

            var lowerCircleSize = new OsuModDifficultyAdjust { CircleSize = { Value = originalCircleSize - 2 } };
            var higherCircleSize = new OsuModDifficultyAdjust { CircleSize = { Value = originalCircleSize + 2 } };

            Assert.That(starRating(lowerCircleSize, new OsuModRelax(), new OsuModUnnerfed()), Is.EqualTo(starRating()).Within(CHECK_PRECISION));
            Assert.That(starRating(higherCircleSize, new OsuModRelax(), new OsuModUnnerfed()), Is.EqualTo(starRating(higherCircleSize)).Within(CHECK_PRECISION));
            Assert.That(starRating(higherCircleSize), Is.GreaterThan(starRating()));
        }

        [Test]
        public void TestEasierModsDoNotLowerScoreMultiplier()
        {
            var calculator = new OsuRuleset().CreateScoreMultiplierCalculator(new ScoreMultiplierContext(GetBeatmap(beatmap_name).BeatmapInfo.Difficulty));

            Assert.That(calculator.CalculateFor(new Mod[] { new OsuModEasy(), new OsuModNoFail(), new OsuModRelax() }), Is.LessThan(1), "sanity check");
            Assert.That(calculator.CalculateFor(new Mod[] { new OsuModEasy(), new OsuModNoFail(), new OsuModRelax(), new OsuModUnnerfed() }), Is.EqualTo(1));

            double hiddenHardRock = calculator.CalculateFor(new Mod[] { new OsuModHidden(), new OsuModHardRock() });

            Assert.That(hiddenHardRock, Is.GreaterThan(1));
            Assert.That(calculator.CalculateFor(new Mod[] { new OsuModHidden(), new OsuModHardRock(), new OsuModRelax(), new OsuModUnnerfed() }), Is.EqualTo(hiddenHardRock));
        }

        [Test]
        public void TestEasierModsDoNotLowerPerformance()
        {
            double noMod = perfectScorePerformance();

            Assert.That(perfectScorePerformance(new OsuModRelax(), new OsuModEasy()), Is.LessThan(noMod), "sanity check: easier mods normally lower pp");
            Assert.That(perfectScorePerformance(new OsuModRelax(), new OsuModEasy(), new OsuModNoFail(), new OsuModUnnerfed()), Is.EqualTo(noMod).Within(CHECK_PRECISION));

            double hidden = perfectScorePerformance(new OsuModHidden());

            Assert.That(hidden, Is.GreaterThan(noMod));
            Assert.That(perfectScorePerformance(new OsuModHidden(), new OsuModRelax(), new OsuModUnnerfed()), Is.EqualTo(hidden).Within(CHECK_PRECISION));
        }

        private double starRating(params Mod[] mods) => CreateDifficultyCalculator(GetBeatmap(beatmap_name)).Calculate(mods).StarRating;

        private double perfectScorePerformance(params Mod[] mods)
        {
            var ruleset = new OsuRuleset();
            var workingBeatmap = GetBeatmap(beatmap_name);
            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

            var attributes = CreateDifficultyCalculator(workingBeatmap).Calculate(mods);

            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(playableBeatmap);

            var score = new ScoreInfo((BeatmapInfo)workingBeatmap.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = mods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics
            };

            return ruleset.CreatePerformanceCalculator()!.Calculate(score, attributes).Total;
        }
    }
}
