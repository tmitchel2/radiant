import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import quantize.QuantizerCelebi;
import quantize.QuantizerMap;
import quantize.QuantizerWu;
import score.Score;

/**
 * Writes the quantize/score fixture from upstream's Java port.
 *
 * <p>Upstream's TypeScript Wsmeans seeds k-means with Math.random(), so its results vary run to run;
 * the Java port seeds java.util.Random with 0x42688 and is reproducible. Radiant follows Java, so its
 * fixture comes from Java. Input: one image per line, pixels as space-separated signed ints.
 * Output: JSON, colours as unsigned 0xAARRGGBB numbers.
 */
public final class QuantizeFixtures {
  public static void main(String[] args) throws IOException {
    List<String> lines = Files.readAllLines(Path.of(args[0]));
    StringBuilder json = new StringBuilder("[");
    for (int n = 0; n < lines.size(); n++) {
      String[] parts = lines.get(n).trim().split(" ");
      int[] pixels = new int[parts.length];
      for (int i = 0; i < parts.length; i++) {
        pixels[i] = (int) Long.parseLong(parts[i]);
      }
      Map<Integer, Integer> celebi = QuantizerCelebi.quantize(pixels, 128);
      if (n > 0) {
        json.append(',');
      }
      json.append("{\"pixels\":").append(colours(toList(pixels)));
      json.append(",\"map\":").append(pairs(new QuantizerMap().quantize(pixels, 0).colorToCount));
      json.append(",\"wu\":").append(colours(new ArrayList<>(new QuantizerWu().quantize(pixels, 128).colorToCount.keySet())));
      json.append(",\"celebi\":").append(pairs(celebi));
      json.append(",\"celebi16\":").append(pairs(QuantizerCelebi.quantize(pixels, 16)));
      json.append(",\"score\":").append(colours(Score.score(celebi)));
      json.append(",\"scoreUnfiltered\":").append(colours(Score.score(celebi, 6, 0xff4285f4, false)));
      json.append(",\"scoreFallback\":").append(colours(Score.score(celebi, 3, 0xff123456, true)));
      json.append('}');
    }
    json.append(']');
    Files.writeString(Path.of(args[1]), json);
  }

  private static List<Integer> toList(int[] values) {
    List<Integer> list = new ArrayList<>(values.length);
    for (int v : values) {
      list.add(v);
    }
    return list;
  }

  private static String colours(List<Integer> values) {
    StringBuilder b = new StringBuilder("[");
    for (int i = 0; i < values.size(); i++) {
      if (i > 0) {
        b.append(',');
      }
      b.append(Integer.toUnsignedLong(values.get(i)));
    }
    return b.append(']').toString();
  }

  // Sorted by unsigned colour so the fixture does not depend on map iteration order.
  private static String pairs(Map<Integer, Integer> map) {
    TreeMap<Long, Integer> sorted = new TreeMap<>();
    map.forEach((argb, count) -> sorted.put(Integer.toUnsignedLong(argb), count));
    StringBuilder b = new StringBuilder("[");
    sorted.forEach((argb, count) -> {
      if (b.length() > 1) {
        b.append(',');
      }
      b.append('[').append(argb).append(',').append(count).append(']');
    });
    return b.append(']').toString();
  }
}
