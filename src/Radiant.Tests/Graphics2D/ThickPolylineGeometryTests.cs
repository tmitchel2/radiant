using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Tests.Graphics2D.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Tests.Graphics2D
{
    /// <summary>
    /// The caps and joins a chain of quads has not got, asserted as pixels rather than as vertices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The defect is a hole, so the test is a hole.</b> A vertex count cannot see a notch — the
    /// wrong geometry emits vertices too. Rasterising through <see cref="VisualTestHelper"/> and
    /// probing the one pixel that is missing states the actual complaint, and
    /// <see cref="TwoChainedThickLinesLeaveANotchOnTheOutsideOfACorner"/> is its falsifier: it is the
    /// old behaviour, it passes, and it is what every assertion below is measured against.
    /// </para>
    /// <para>
    /// The probe points were established by rasterising the L below and reading the pixels out, not
    /// by deriving them: (92,28) is outside both quads and inside the join, and (27,30) is past the
    /// free end.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ThickPolylineGeometryTests
    {
        private static readonly Vector4 Fill = new(1f, 1f, 1f, 1f);

        // An L: right along the top, then down the right. The corner is at (90,30).
        private static readonly Vector2[] Corner =
        [
            new(30f, 30f), new(90f, 30f), new(90f, 90f),
        ];

        private const int Outside = 92;
        private const int Above = 28;

        [TestMethod]
        public void TwoChainedThickLinesLeaveANotchOnTheOutsideOfACorner()
        {
            using var helper = new VisualTestHelper();

            helper.Renderer.DrawThickLine(Corner[0], Corner[1], 12f, Fill);
            helper.Renderer.DrawThickLine(Corner[1], Corner[2], 12f, Fill);

            using var image = helper.Rasterize();

            Assert.IsTrue(Lit(image, 60, 30), "the horizontal arm is missing.");
            Assert.IsTrue(Lit(image, 90, 60), "the vertical arm is missing.");
            Assert.IsFalse(
                Lit(image, Outside, Above),
                "the notch is gone, so every join assertion in this file is vacuous.");
        }

        [TestMethod]
        public void ARoundJoinFillsTheNotch() => AssertJoinFillsTheNotch(LineJoin.Round);

        [TestMethod]
        public void AMiterJoinFillsTheNotch() => AssertJoinFillsTheNotch(LineJoin.Miter);

        [TestMethod]
        public void ABevelJoinFillsTheNotch() => AssertJoinFillsTheNotch(LineJoin.Bevel);

        [TestMethod]
        public void AButtCapStopsAtTheEndpoint()
        {
            using var image = Stroke(LineCap.Butt, LineJoin.Round);

            Assert.IsTrue(Lit(image, 33, 30), "the stroke does not reach its own end.");
            Assert.IsFalse(Lit(image, 27, 30), "a butt cap reached past the endpoint.");
        }

        [TestMethod]
        public void ARoundCapReachesHalfAWidthPastTheEndpoint()
        {
            using var image = Stroke(LineCap.Round, LineJoin.Round);

            Assert.IsTrue(Lit(image, 27, 30), "a round cap did not reach past the endpoint.");

            // Half a width is 6, so 8 past the end is outside the disc whichever way it is drawn.
            Assert.IsFalse(Lit(image, 22, 30), "a round cap reached further than its own radius.");
        }

        [TestMethod]
        public void ASquareCapReachesTheCornersARoundOneDoesNot()
        {
            using var round = Stroke(LineCap.Round, LineJoin.Round);
            using var square = Stroke(LineCap.Square, LineJoin.Round);

            // The corner of the square cap: half a width back and half a width out. A disc of the
            // same radius cannot reach it, which is what tells the two apart.
            Assert.IsTrue(Lit(square, 25, 35), "a square cap is missing its corner.");
            Assert.IsFalse(Lit(round, 25, 35), "a round cap reached a square cap's corner.");
        }

        [TestMethod]
        public void AMiterReachesFurtherThanABevelOnTheSameCorner()
        {
            using var miter = Stroke(LineCap.Butt, LineJoin.Miter);
            using var bevel = Stroke(LineCap.Butt, LineJoin.Bevel);

            // The outermost point of a right-angle miter is (96,24). A bevel cuts that corner off.
            Assert.IsTrue(Lit(miter, 95, 25), "the miter did not reach its own point.");
            Assert.IsFalse(Lit(bevel, 95, 25), "the bevel reached the miter's point.");
        }

        [TestMethod]
        public void ASharpCornerFallsBackToABevelRatherThanThrowingASpike()
        {
            // Nearly doubled back: the miter reaches half / sin(theta/2), which is unbounded. With a
            // limit of 4 it must be cut off, or a hairpin turn throws a needle across the viewport.
            Vector2[] hairpin = [new(30f, 64f), new(100f, 64f), new(31f, 70f)];

            using var helper = new VisualTestHelper();

            helper.Renderer.DrawThickPolyline(
                hairpin, 12f, Fill, LineCap.Butt, LineJoin.Miter, miterLimit: 4f);

            using var image = helper.Rasterize();

            // 4 half-widths past the corner is 24, so nothing may be lit beyond that.
            Assert.IsFalse(
                Lit(image, 127, 64), "the miter spike was not limited.");
        }

        [TestMethod]
        public void ConsecutiveDuplicatePointsDoNotProduceNaNGeometry()
        {
            Vector2[] doubled =
            [
                new(30f, 30f), new(30f, 30f), new(90f, 30f), new(90f, 30f), new(90f, 90f),
            ];

            using var helper = new VisualTestHelper();

            helper.Renderer.DrawThickPolyline(doubled, 12f, Fill, LineCap.Round, LineJoin.Round);

            foreach (var vertex in helper.Renderer.FilledVertices)
            {
                Assert.IsFalse(
                    float.IsNaN(vertex.Position.X) || float.IsNaN(vertex.Position.Y),
                    "a repeated point normalised a zero vector.");
            }

            using var image = helper.Rasterize();

            Assert.IsTrue(Lit(image, Outside, Above), "the join is missing after a repeated point.");
        }

        [TestMethod]
        public void APathOfOnePointIsADotWhenItIsRoundCappedAndNothingOtherwise()
        {
            // A track whose two ends are the same point is a real thing on a real board, and the
            // artefact renderer draws it as a dot rather than as nothing.
            Vector2[] single = [new(64f, 64f)];

            using var round = new VisualTestHelper();
            round.Renderer.DrawThickPolyline(single, 12f, Fill, LineCap.Round, LineJoin.Round);

            using var lit = round.Rasterize();

            // JUST OFF CENTRE, and that is not fussiness. Every triangle of a fan meets at the
            // centre vertex, and the rasteriser's fill rule drops a pixel that lands exactly on a
            // shared edge -- so a small disc reads as unlit at its own middle and lit everywhere
            // else. Probing the centre would have failed on correct geometry.
            Assert.IsTrue(Lit(lit, 64, 61), "a round-capped point drew nothing.");

            using var butt = new VisualTestHelper();
            butt.Renderer.DrawThickPolyline(single, 12f, Fill, LineCap.Butt, LineJoin.Round);

            Assert.AreEqual(0, butt.Renderer.FilledVertices.Count);
        }

        [TestMethod]
        public void NothingIsDrawnForAnEmptyPathOrANonPositiveWidth()
        {
            var renderer = new Renderer2D();

            renderer.DrawThickPolyline([], 12f, Fill);
            renderer.DrawThickPolyline(Corner, 0f, Fill);
            renderer.DrawThickPolyline(Corner, -3f, Fill);

            Assert.AreEqual(0, renderer.FilledVertices.Count);
        }

        [TestMethod]
        public void AStraightPathNeedsNoJoinAndEmitsNoneOfIt()
        {
            var straight = new Renderer2D();
            straight.DrawThickPolyline(
                [new(10f, 20f), new(50f, 20f), new(90f, 20f)], 8f, Fill, LineCap.Butt, LineJoin.Round);

            var plain = new Renderer2D();
            plain.DrawThickLine(new Vector2(10f, 20f), new Vector2(50f, 20f), 8f, Fill);
            plain.DrawThickLine(new Vector2(50f, 20f), new Vector2(90f, 20f), 8f, Fill);

            Assert.AreEqual(
                plain.FilledVertices.Count,
                straight.FilledVertices.Count,
                "a collinear corner was given a join it does not need.");
        }

        [TestMethod]
        public void ARoundCapReachesPastEveryFreeEndWhicheverWayThePathRuns()
        {
            // ONE ORIENTATION IS NOT A TEST OF THIS. A cap is exactly pi, the sweep where "shortest"
            // has no answer, so the arbitrary choice can be outward at one end of a line and inward
            // at the other -- which is precisely what it was doing, correct at the end and wrong at
            // the start of the very same path.
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1) })
            {
                var from = new Vector2(64f - (dx * 20f), 64f - (dy * 20f));
                var to = new Vector2(64f + (dx * 20f), 64f + (dy * 20f));

                using var helper = new VisualTestHelper();

                helper.Renderer.DrawThickPolyline(
                    [from, to], 12f, Fill, LineCap.Round, LineJoin.Round);

                using var image = helper.Rasterize();

                // Three past each end, well inside a radius of six.
                var beyond = Vector2.Normalize(from - to) * 3f;

                Assert.IsTrue(
                    Lit(image, (int)(from.X + beyond.X), (int)(from.Y + beyond.Y)),
                    $"the start cap is missing when the path runs ({dx},{dy}).");

                Assert.IsTrue(
                    Lit(image, (int)(to.X - beyond.X), (int)(to.Y - beyond.Y)),
                    $"the end cap is missing when the path runs ({dx},{dy}).");
            }
        }

        [TestMethod]
        public void AClosedRingIsJoinedAtItsSeamRatherThanCapped()
        {
            // The seam is where the caller repeated the first point. Left as two caps it notches
            // like any other corner, and it is the one corner a reader did not ask for.
            Vector2[] ring =
            [
                new(40f, 40f), new(88f, 40f), new(88f, 88f), new(40f, 88f), new(40f, 40f),
            ];

            foreach (var join in new[] { LineJoin.Round, LineJoin.Miter, LineJoin.Bevel })
            {
                using var helper = new VisualTestHelper();

                helper.Renderer.DrawThickPolyline(ring, 12f, Fill, LineCap.Butt, join);

                using var image = helper.Rasterize();

                // Just outside all four corners, the seam among them.
                foreach (var (x, y) in new[] { (42, 38), (86, 38), (86, 90), (42, 90) })
                {
                    Assert.IsTrue(Lit(image, x, y), $"a {join} ring notches at ({x},{y}).");
                }
            }
        }

        [TestMethod]
        public void AnUnreasonableMiterLimitStillProducesFiniteGeometry()
        {
            // NaN fails every comparison, so an unvalidated limit lets the spike through the test
            // meant to stop it -- and NaN in a vertex buffer draws a triangle across the viewport or
            // nothing at all, neither of which looks like a bug in a limit.
            Vector2[] hairpin = [new(30f, 64f), new(100f, 64f), new(31f, 70f)];

            foreach (var limit in new[] { float.NaN, -1f, 0f, float.PositiveInfinity })
            {
                var renderer = new Renderer2D();

                renderer.DrawThickPolyline(hairpin, 12f, Fill, LineCap.Round, LineJoin.Miter, limit);

                foreach (var vertex in renderer.FilledVertices)
                {
                    Assert.IsTrue(
                        float.IsFinite(vertex.Position.X) && float.IsFinite(vertex.Position.Y),
                        $"a miter limit of {limit} produced non-finite geometry.");
                }
            }
        }

        [TestMethod]
        public void ADoubledBackPathIsRoundedRatherThanLeftOpen()
        {
            // A reversal has no "outside" in the usual sense, but a round stroke still turns around
            // there, and the board's router does lay them.
            Vector2[] back = [new(40f, 64f), new(90f, 64f), new(40f, 64f)];

            using var helper = new VisualTestHelper();

            helper.Renderer.DrawThickPolyline(back, 12f, Fill, LineCap.Round, LineJoin.Round);

            using var image = helper.Rasterize();

            Assert.IsTrue(Lit(image, 93, 64), "the turnaround is not rounded.");
        }

        [TestMethod]
        public void ANullPathIsRefusedRatherThanIgnored() =>
            Assert.ThrowsException<System.ArgumentNullException>(
                () => new Renderer2D().DrawThickPolyline(null!, 12f, Fill));

        private static void AssertJoinFillsTheNotch(LineJoin join)
        {
            using var image = Stroke(LineCap.Butt, join);

            Assert.IsTrue(
                Lit(image, Outside, Above), $"a {join} join left the outside of the corner open.");
        }

        private static Image<Rgba32> Stroke(LineCap cap, LineJoin join)
        {
            using var helper = new VisualTestHelper();

            helper.Renderer.DrawThickPolyline(Corner, 12f, Fill, cap, join);

            return helper.Rasterize();
        }

        private static bool Lit(Image<Rgba32> image, int x, int y) => image[x, y].R > 127;
    }
}
