// Generates reference MSDFs with upstream msdfgen, for the parity tests of Radiant.Text's port
// (src/Radiant.Text.Tests/MsdfParityTests.cs). Built and run by generate.sh.
//
// Reads a shape (one command a line: "M x y", "L x y", "Q cx cy x y", "C c1x c1y c2x c2y x y",
// "Z"; "#" starts a comment; the first non-comment line is "scale range"), builds it as msdfgen
// imports a font outline (ext/import-font.cpp), prepares it as msdfgen's main.cpp does, frames
// it as Radiant's MsdfGenerator.Generate does, and writes:
//
//   width height left top scale range
//   one line per texel row from the bottom: r g b r g b ... as %.9g floats
//
// Usage: fixture < input.shape > output.msdf

#include <cmath>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>
#include "msdfgen.h"
#include "core/ShapeDistanceFinder.h"

using namespace msdfgen;

int main() {
    char line[4096];
    double scale = 0, range = 0;
    bool header = false;
    Shape shape;
    Contour *contour = NULL;
    Point2 position, start;

    // Closes the current contour back to its start, as FreeType's decomposition does.
    auto close = [&]() {
        if (contour && !contour->edges.empty() && position != start) {
            contour->addEdge(EdgeHolder(position, start));
            position = start;
        }
    };

    while (fgets(line, sizeof line, stdin)) {
        char *p = line;
        while (*p == ' ' || *p == '\t') ++p;
        if (*p == '#' || *p == '\n' || *p == '\r' || !*p) continue;
        if (!header) {
            if (sscanf(p, "%lf %lf", &scale, &range) != 2) return 1;
            header = true;
            continue;
        }
        double v[6];
        switch (*p) {
            case 'M':
                sscanf(p+1, "%lf %lf", &v[0], &v[1]);
                close();
                if (!(contour && contour->edges.empty()))
                    contour = &shape.addContour();
                position = start = Point2(v[0], v[1]);
                break;
            case 'L': {
                sscanf(p+1, "%lf %lf", &v[0], &v[1]);
                Point2 endpoint(v[0], v[1]);
                if (contour && endpoint != position) {
                    contour->addEdge(EdgeHolder(position, endpoint));
                    position = endpoint;
                }
                break;
            }
            case 'Q': {
                sscanf(p+1, "%lf %lf %lf %lf", &v[0], &v[1], &v[2], &v[3]);
                Point2 endpoint(v[2], v[3]);
                if (contour && endpoint != position) {
                    contour->addEdge(EdgeHolder(position, Point2(v[0], v[1]), endpoint));
                    position = endpoint;
                }
                break;
            }
            case 'C': {
                sscanf(p+1, "%lf %lf %lf %lf %lf %lf", &v[0], &v[1], &v[2], &v[3], &v[4], &v[5]);
                Point2 c1(v[0], v[1]), c2(v[2], v[3]), endpoint(v[4], v[5]);
                if (contour && (endpoint != position || crossProduct(c1-endpoint, c2-endpoint))) {
                    contour->addEdge(EdgeHolder(position, c1, c2, endpoint));
                    position = endpoint;
                }
                break;
            }
            case 'Z':
                close();
                break;
        }
    }
    close();
    if (!shape.contours.empty() && shape.contours.back().edges.empty())
        shape.contours.pop_back();
    if (!shape.validate()) return 2;

    // As main.cpp: normalize, guess the winding from a point outside the bounds, colour.
    shape.normalize();
    Shape::Bounds bounds = shape.getBounds();
    Point2 outer(bounds.l-(bounds.r-bounds.l)-1, bounds.b-(bounds.t-bounds.b)-1);
    if (SimpleTrueShapeDistanceFinder::oneShotDistance(shape, outer) > 0) {
        for (std::vector<Contour>::iterator c = shape.contours.begin(); c != shape.contours.end(); ++c)
            c->reverse();
    }
    edgeColoringSimple(shape, 3.0, 0);

    // As MsdfGenerator.Generate: whole pixels from the pen, half the range around the shape.
    double pad = range/2;
    int left = (int) floor(bounds.l*scale-pad);
    int right = (int) ceil(bounds.r*scale+pad);
    int top = (int) floor(-bounds.t*scale-pad);
    int bottom = (int) ceil(-bounds.b*scale+pad);
    int width = right-left, height = bottom-top;

    Bitmap<float, 3> msdf(width, height);
    Projection projection(Vector2(scale), Vector2(-left/scale, bottom/scale));
    generateMSDF(msdf, shape, SDFTransformation(projection, DistanceMapping(Range(range/scale))), MSDFGeneratorConfig(true));

    printf("%d %d %d %d %.17g %.17g\n", width, height, left, top, scale, range);
    for (int y = 0; y < height; ++y) {
        for (int x = 0; x < width; ++x) {
            const float *t = msdf(x, y);
            printf(x ? " %.9g %.9g %.9g" : "%.9g %.9g %.9g", t[0], t[1], t[2]);
        }
        printf("\n");
    }
    return 0;
}
