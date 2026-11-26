// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// The in-game pp counter must not be lowered by easier mods (such as a lowered circle size) while <see cref="ModUnnerfed"/> is active.
    /// </summary>
    public partial class TestScenePerformancePointsCounterUnnerfed : OsuTestScene
    {
        private readonly List<(ScoreProcessor processor, IBeatmap beatmap)> plays = new List<(ScoreProcessor, IBeatmap)>();

        private PerformancePointsCounter noModCounter = null!;
        private PerformancePointsCounter unnerfedCounter = null!;
        private PerformancePointsCounter loweredCircleSizeCounter = null!;

        [Test]
        public void TestLoweredCircleSizeDoesNotLowerPerformance()
        {
            AddStep("set up counters", () =>
            {
                var ruleset = new OsuRuleset();
                var working = CreateWorkingBeatmap(ruleset.RulesetInfo);
                Beatmap.Value = working;

                float circleSize = working.BeatmapInfo.Difficulty.CircleSize;

                plays.Clear();

                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        createCounter(ruleset, working, Array.Empty<Mod>(), out noModCounter),
                        createCounter(ruleset, working, new Mod[] { lowerCircleSize(circleSize), new OsuModRelax(), new OsuModUnnerfed() }, out unnerfedCounter),
                        createCounter(ruleset, working, new Mod[] { lowerCircleSize(circleSize) }, out loweredCircleSizeCounter),
                    }
                };
            });

            AddUntilStep("wait for counters to load", () => noModCounter.IsLoaded && unnerfedCounter.IsLoaded && loweredCircleSizeCounter.IsLoaded);

            AddStep("hit every object perfectly", () =>
            {
                foreach (var (processor, beatmap) in plays)
                {
                    foreach (var hitObject in beatmap.HitObjects)
                    {
                        if (hitObject is not HitCircle)
                            continue;

                        processor.ApplyResult(new OsuJudgementResult(hitObject, hitObject.Judgement) { Type = hitObject.Judgement.MaxResult });
                    }
                }
            });

            AddUntilStep("no mod counter has value", () => noModCounter.Current.Value, () => Is.GreaterThan(0));
            AddUntilStep("lowered circle size counter is lower", () => loweredCircleSizeCounter.Current.Value, () => Is.LessThan(noModCounter.Current.Value));
            AddUntilStep("unnerfed counter matches no mod", () => unnerfedCounter.Current.Value, () => Is.EqualTo(noModCounter.Current.Value));
        }

        private static OsuModDifficultyAdjust lowerCircleSize(float circleSize) => new OsuModDifficultyAdjust { CircleSize = { Value = circleSize - 2 } };

        private Drawable createCounter(OsuRuleset ruleset, WorkingBeatmap working, Mod[] mods, out PerformancePointsCounter counter)
        {
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

            var processor = ruleset.CreateScoreProcessor();
            processor.Mods.Value = mods;
            processor.ApplyBeatmap(playable);

            plays.Add((processor, playable));

            var gameplayState = new GameplayState(playable, ruleset, mods, scoreProcessor: processor);

            return new DependencyProvidingContainer
            {
                AutoSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(ScoreProcessor), processor),
                    (typeof(GameplayState), gameplayState),
                },
                Child = counter = new ArgonPerformancePointsCounter()
            };
        }
    }
}
