// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Online.API;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// This fork must never log in to the official osu! servers.
    /// </summary>
    [TestFixture]
    public class OfficialServerSafetyTest
    {
        [Test]
        public void TestGameAlwaysUsesDevelopmentServer()
        {
            var game = new OsuGameBase();

            Assert.That(game.UseDevelopmentServer, Is.True);
            Assert.That(game.CreateEndpoints(), Is.InstanceOf<DevelopmentEndpointConfiguration>());
            Assert.That(APIAccess.IsOfficialServer(game.CreateEndpoints().APIUrl), Is.False);
        }

        [TestCase("https://osu.ppy.sh", true)]
        [TestCase("https://OSU.ppy.sh/", true)]
        [TestCase("https://dev.ppy.sh", false)]
        [TestCase("not a url", false)]
        public void TestOfficialServerDetection(string url, bool expected)
        {
            Assert.That(APIAccess.IsOfficialServer(url), Is.EqualTo(expected));
        }

        [Test]
        public void TestProductionEndpointIsDetected()
        {
            Assert.That(APIAccess.IsOfficialServer(new ProductionEndpointConfiguration().APIUrl), Is.True);
        }
    }
}
