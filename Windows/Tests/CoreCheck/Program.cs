// Logik-Check (EXIF, Duplikate, Sortieren, Aufräumen): dotnet run --project Tests/CoreCheck [-- <beliebige.jpg>]
using SaveTheDate;

// Ohne Argument wird ein winziges eingebettetes JPEG verwendet (so läuft der Check auch in der CI).
var src = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".jpg");
if (args.Length == 0) File.WriteAllBytes(src, Convert.FromBase64String("/9j/wAARCAAQABADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9sAQwACAgICAgIDAgIDBQMDAwUGBQUFBQYIBgYGBgYICggICAgICAoKCgoKCgoKDAwMDAwMDg4ODg4PDw8PDw8PDw8P/9sAQwECAgIEBAQHBAQHEAsJCxAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQ/90ABAAB/9oADAMBAAIRAxEAPwD6e8R+IfBfkyWesana2UqoWIllVPlA56kV8N/GlNM8oDw7Nb6gsybxMsqG3QZYuWkBxkKMhevHSu68c+J/DcPjW68H+Prq2tbW8ubYaLJbZFwHCAuZcLjmQ4UMdpBwQRXxR4w8d6PqepWr6NBMltaQyxXVrNxmdHdDmTadxwqnhVwScZ5r57LcVmlDBt06nvW0sv8Agn1eNo5fUxMfb07pPq/+Af/Z"));
var f = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".jpg");
File.Copy(src, f);
var taken = new DateTime(2018, 3, 4, 5, 6, 7);

ExifJpeg.Update(f, taken, new GeoEdit(GeoKind.Set, -33.8688, 151.2093));
var m = ExifJpeg.Read(f);
Assert(m.Taken == taken, "Datum");
Assert(Math.Abs(m.Lat!.Value + 33.8688) < 1e-4 && Math.Abs(m.Lon!.Value - 151.2093) < 1e-4, "GPS");

ExifJpeg.Update(f, null, new GeoEdit(GeoKind.Remove));
m = ExifJpeg.Read(f);
Assert(m.Taken == taken && m.Lat == null, "Entfernen");

ExifJpeg.Update(f, taken.AddDays(1), new GeoEdit(GeoKind.Keep));
Assert(ExifJpeg.Read(f).Taken == taken.AddDays(1), "Nur Datum");

// Bilddaten unverändert: alles ab dem Start-of-Scan (Segmente korrekt übersprungen) muss identisch sein
static byte[] Scan(byte[] d)
{
    var p = 2;
    while (d[p + 1] != 0xDA) p += 2 + (d[p + 2] << 8 | d[p + 3]);
    return d[p..];
}
Assert(Scan(File.ReadAllBytes(src)).AsSpan().SequenceEqual(Scan(File.ReadAllBytes(f))), "Bilddaten");
Console.WriteLine("EXIF ok");

// ---- Duplikate: gleicher Inhalt, anderer Name; gleiche Größe aber anderer Inhalt hinter den ersten 64 KB ----
static string Temp() { var d = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
static void W(string dir, string rel, byte[] data, DateTime? date = null)
{
    var p = Path.Combine(dir, rel);
    Directory.CreateDirectory(Path.GetDirectoryName(p)!);
    File.WriteAllBytes(p, data);
    if (date is { } t) File.SetLastWriteTime(p, t);
}
var all = new ScanOptions(true, false, true);
var d1 = Temp();
var big = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray();
var other = (byte[])big.Clone();
other[^1] ^= 1;
W(d1, "a.jpg", big); W(d1, "x/ganz-anders.jpg", big); W(d1, "c.jpg", other);
W(d1, "d.txt", [1, 2, 3]); W(d1, "e.txt", [1, 2, 3]); W(d1, "leer1", []); W(d1, "leer2", []);
var groups = Dupes.Find(Scanner.Run([d1], all, default), null, default)
    .Select(g => string.Join("|", g.Select(i => i.Name).Order())).Order().ToList();
Assert(groups.SequenceEqual(new[] { "a.jpg|ganz-anders.jpg", "d.txt|e.txt" }), "Duplikate: " + string.Join(";", groups));
Console.WriteLine("Duplikate ok");

// ---- Sortieren: JJJJ/MM, Namenskonflikt, Undo, leere Ordner ----
var d2 = Temp();
var jul = new DateTime(2019, 7, 13, 12, 0, 0);
var feb = new DateTime(2020, 2, 18, 12, 0, 0);
W(d2, "alt/IMG_1.jpg", [1, 1], jul); W(d2, "neu/IMG_1.jpg", [2, 2], jul);
W(d2, "neu/IMG_2.jpg", [3, 3], feb); W(d2, "neu/notiz.txt", [4], feb);
var outDir = Path.Combine(d2, "Sortiert");
var plan = Organizer.Plan(Scanner.Run([d2], all, default), outDir, Pattern.YearMonth, DateSource.Modified, true);
var rel = plan.Select(m => m.Rel.Replace('\\', '/')).Order().ToList();
Assert(rel.SequenceEqual(new[] { "2019/07/IMG_1 (2).jpg", "2019/07/IMG_1.jpg", "2020/02/IMG_2.jpg" }), "Plan: " + string.Join(";", rel));
var r = Organizer.Run(plan, false, true, [d2], null);
Assert(r.Ops.Count == 3 && r.Errors.Count == 0, "Run");
Assert(File.Exists(Path.Combine(outDir, "2020", "02", "IMG_2.jpg")), "Ziel");
Assert(!Directory.Exists(Path.Combine(d2, "alt")), "leerer Ordner entfernt");
Assert(File.Exists(Path.Combine(d2, "neu", "notiz.txt")), "notiz bleibt");
Assert(Organizer.Undo(r.Ops, outDir).Count == 0, "Undo");
Assert(File.Exists(Path.Combine(d2, "alt", "IMG_1.jpg")) && !Directory.Exists(Path.Combine(outDir, "2019")), "Undo-Ergebnis");
W(outDir, "2020/02/IMG_2.jpg", [3, 3], feb);
var again = Organizer.Plan(Scanner.Run([Path.Combine(d2, "neu")], all, default), outDir, Pattern.YearMonth, DateSource.Modified, true);
Assert(again.First(m => m.Item.Name == "IMG_2.jpg").Skip != null, "identische Datei am Ziel");
Console.WriteLine("Sortieren ok");

// ---- Aufräumen ----
var d3 = Temp();
W(d3, "foto.jpg", [1]);
foreach (var n in new[] { "desktop.ini", ".DS_Store", "._foto.jpg", "x.tmp" }) W(d3, n, [0]);
W(d3, "nurmuell/Thumbs.db", [0]);
Directory.CreateDirectory(Path.Combine(d3, "leer", "tief"));
var found = Cleanup.Scan([d3], Cleanup.Patterns("*.TMP"), true, default).Select(j => Path.GetFileName(j.Path)).ToList();
foreach (var n in new[] { "desktop.ini", ".DS_Store", "._foto.jpg", "x.tmp", "Thumbs.db", "nurmuell", "leer", "tief" }) Assert(found.Contains(n), n + " fehlt");
Assert(!found.Contains("foto.jpg") && found.IndexOf("tief") < found.IndexOf("leer"), "Reihenfolge");
Assert(!Cleanup.Scan([d3], [], false, default).Any(j => j.Reason == "Leerer Ordner"), "ohne leere Ordner");
Console.WriteLine("Aufräumen ok");
Console.WriteLine("OK");

static void Assert(bool c, string what) { if (!c) { Console.WriteLine("FEHLER: " + what); Environment.Exit(1); } }
