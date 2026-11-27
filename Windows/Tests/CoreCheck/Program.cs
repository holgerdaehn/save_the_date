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

// Wiederholte Änderungen dürfen den EXIF-Block nicht wachsen lassen
var size0 = new FileInfo(f).Length;
for (var i = 0; i < 200; i++) ExifJpeg.Update(f, taken.AddDays(i % 2 + 1), new GeoEdit(GeoKind.Set, 50 + i % 2, 8));
m = ExifJpeg.Read(f);
Assert(m.Taken == taken.AddDays(2) && Math.Abs(m.Lat!.Value - 51) < 1e-4, "nach 200 Änderungen");
Assert(new FileInfo(f).Length < size0 + 400, $"EXIF wächst: {size0} -> {new FileInfo(f).Length}");

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
foreach (var n in new[] { "desktop.ini", ".DS_Store", "._foto.jpg", "._weg.jpg", "x.tmp" }) W(d3, n, [0]);
W(d3, "nurmuell/Thumbs.db", [0]);
Directory.CreateDirectory(Path.Combine(d3, "leer", "tief"));
var found = Cleanup.Scan([d3], Cleanup.Patterns("*.TMP"), true, default).Select(j => Path.GetFileName(j.Path)).ToList();
foreach (var n in new[] { "desktop.ini", ".DS_Store", "._weg.jpg", "x.tmp", "Thumbs.db", "nurmuell", "leer", "tief" }) Assert(found.Contains(n), n + " fehlt");
Assert(!found.Contains("foto.jpg") && !found.Contains("._foto.jpg") && found.IndexOf("tief") < found.IndexOf("leer"), "Reihenfolge");
Assert(!Cleanup.Scan([d3], [], false, default).Any(j => j.Reason == "Leerer Ordner"), "ohne leere Ordner");
var dPrune = Temp();
W(dPrune, "a/__MACOSX/x", [1]);
W(dPrune, "b/.DS_Store", [0]);
Assert(Organizer.PruneEmpty([Path.Combine(dPrune, "a"), Path.Combine(dPrune, "b")], [dPrune]) == 1 && File.Exists(Path.Combine(dPrune, "a", "__MACOSX", "x")), "Junk-Ordner bleibt");
Console.WriteLine("Aufräumen ok");

// ---- Datum aus Dateiname ----
static DateTime D(int y, int mo, int d, int h = 12, int mi = 0, int s = 0) => new(y, mo, d, h, mi, s);
Assert(NameDate.Parse("IMG-20190713-WA0001.jpg") == D(2019, 7, 13), "WhatsApp");
Assert(NameDate.Parse("PXL_20210101_120533123.jpg") == D(2021, 1, 1, 12, 5, 33), "Pixel");
Assert(NameDate.Parse("Bildschirmfoto 2020-02-18 um 14.22.05.png") == D(2020, 2, 18, 14, 22, 5), "Bildschirmfoto");
Assert(NameDate.Parse("Screenshot_20200218-142205.png") == D(2020, 2, 18, 14, 22, 5), "Screenshot");
Assert(NameDate.Parse("2019-07-13 14.22.05.jpg") == D(2019, 7, 13, 14, 22, 5), "mit Trennzeichen");
Assert(NameDate.Parse("signal-1612345678901.jpg") == DateTimeOffset.FromUnixTimeMilliseconds(1612345678901).LocalDateTime, "Epoche (ms)");
Assert(NameDate.Parse("19860701_120000000_iOS 1.jpg") == D(1986, 7, 1, 12, 0, 0), "vor 1990");
Assert(NameDate.Parse("IMG_20190230_101010.jpg") == null, "30. Februar");
Assert(NameDate.Parse("IMG_4711.jpg") == null && NameDate.Parse("foto 120190713123.jpg") == null, "kein Datum");
Console.WriteLine("Dateiname-Datum ok");

// ---- MP4/MOV-Datum ----
static byte[] BE(ulong v, int n) => Enumerable.Range(0, n).Select(i => (byte)(v >> (8 * (n - 1 - i)))).ToArray();
static byte[] Box(string t, params byte[][] body) { var b = body.SelectMany(x => x).ToArray(); return [.. BE((ulong)b.Length + 8, 4), .. System.Text.Encoding.ASCII.GetBytes(t), .. b]; }
var mvhd = Box("mvhd", [0, 0, 0, 0], BE(0, 4), BE(0, 4), BE(1000, 4), BE(0, 4));
var tkhd = Box("tkhd", [1, 0, 0, 3], BE(0, 8), BE(0, 8), BE(1, 4));
var mdhd = Box("mdhd", [0, 0, 0, 0], BE(0, 4), BE(0, 4), BE(600, 4));
var mp4 = Path.Combine(Temp(), "v.mp4");
var mp4Bytes = Box("ftyp", System.Text.Encoding.ASCII.GetBytes("isom"), BE(0, 4))
    .Concat(Box("moov", mvhd, Box("trak", tkhd, Box("mdia", mdhd)))).Concat(Box("mdat", [1, 2, 3, 4, 5])).ToArray();
File.WriteAllBytes(mp4, mp4Bytes);
Assert(Mp4.Read(mp4) == null, "MP4 ohne Datum");
var vt = new DateTime(2020, 9, 13, 14, 26, 40);
Mp4.Write(mp4, vt);
Assert(Mp4.Read(mp4) == vt, "MP4 Datum");
var mp4After = File.ReadAllBytes(mp4);
Assert(mp4After.Length == mp4Bytes.Length && mp4After[^13..].SequenceEqual(mp4Bytes[^13..]), "mdat unberührt");
var junk = Path.Combine(Temp(), "x.mp4");
File.WriteAllBytes(junk, [1, 2, 3, 4, 5, 6, 7, 8]);
var threw = false;
try { Mp4.Write(junk, vt); } catch (IOException) { threw = true; }
Assert(threw, "kein MP4 → Fehler");
Console.WriteLine("MP4 ok");

// ---- GPX ----
var gpxFile = Path.Combine(Temp(), "t.gpx");
File.WriteAllText(gpxFile, """
    <?xml version="1.0"?><gpx xmlns="http://www.topografix.com/GPX/1/1"><trk><trkseg>
    <trkpt lat="52.0" lon="13.0"><time>2024-07-13T10:00:00Z</time></trkpt>
    <trkpt lat="52.2" lon="13.2"><time>2024-07-13T10:10:00.500Z</time></trkpt>
    <trkpt lat="53.0" lon="14.0"><time>2024-07-13T15:00:00+02:00</time></trkpt>
    </trkseg></trk></gpx>
    """);
var pts = Gpx.Parse([gpxFile]);
Assert(pts.Count == 3, "GPX Punkte");
var t0 = DateTimeOffset.Parse("2024-07-13T10:00:00Z");
var mid = Gpx.Locate(pts, t0.AddMinutes(5), TimeSpan.FromMinutes(15));
Assert(mid is { } mm && Math.Abs(mm.Lat - 52.1) < 0.001 && Math.Abs(mm.Lon - 13.1) < 0.001, "interpoliert");
Assert(Gpx.Locate(pts, t0.AddMinutes(-2), TimeSpan.FromMinutes(5))?.Lat == 52.0, "nächster Punkt");
Assert(Gpx.Locate(pts, t0.AddHours(-1), TimeSpan.FromMinutes(5)) == null && Gpx.Locate(pts, t0.AddHours(2), TimeSpan.FromMinutes(5)) == null, "Lücke");
Assert(Gpx.Instant(new DateTime(2024, 7, 13, 12, 0, 0), 2) == t0, "Kamera-Zeitzone");
Console.WriteLine("GPX ok");

// ---- Umbenennen ----
var d4 = Temp();
var t1 = new DateTime(2024, 7, 13, 14, 22, 5);
W(d4, "IMG_1.JPG", [1], t1); W(d4, "IMG_2.jpg", [2], t1); W(d4, "2024-07-13_14-22-05.jpg", [3], new DateTime(2024, 7, 14, 12, 0, 0)); W(d4, "notiz.txt", [4], t1);
var rp = Renamer.Plan(Scanner.Run([d4], all, default), Renamer.DefaultTemplate, DateSource.Modified, true, true);
var rn = rp.Where(m => m.Skip == null).Select(m => Path.GetFileName(m.Dst)).Order().ToList();
Assert(rn.SequenceEqual(new[] { "2024-07-13_14-22-05 (2).jpg", "2024-07-13_14-22-05 (3).jpg", "2024-07-14_12-00-00.jpg" }), "Umbenennen: " + string.Join(";", rn));
Assert(!rp.Any(m => m.Item.Name == "notiz.txt"), "nur Medien");
var rr = Renamer.Run(rp, null);
Assert(rr.Ops.Count == 3 && rr.Errors.Count == 0 && File.Exists(Path.Combine(d4, "2024-07-13_14-22-05 (2).jpg")), "Umbenennen Run");
Assert(Renamer.Undo(rr.Ops).Count == 0 && File.Exists(Path.Combine(d4, "IMG_1.JPG")), "Umbenennen Undo");
Assert(Renamer.Render("{Nr} {Name}: a/b", null, "x", 7, 12) == "007 x ab", "Render");
Console.WriteLine("Umbenennen ok");

// ---- Übernehmen, Filter, Rückgängig, Ähnliche, Sortier-Vorgaben, CSV ----
var d5 = Temp();
W(d5, "IMG-20190713-WA0001.jpg", [1], new DateTime(2024, 1, 1)); W(d5, "notiz.txt", [2], new DateTime(2023, 1, 2));
var its = Scanner.Run([d5], all, default);
var wa = its.First(i => i.Name.StartsWith("IMG")); var nt = its.First(i => i.Name == "notiz.txt");
var cs = new Settings { Mode = Mode.Copy, Origin = Origin.Name, SetCreated = true };
var pl = Planner.Make(its, cs);
Assert(pl[wa].Created == D(2019, 7, 13) && pl[nt].IsEmpty, "Übernehmen: Dateiname");
cs.Origin = Origin.Oldest;
Assert(Planner.Make(its, cs)[nt].Created == new DateTime(2023, 1, 2), "Übernehmen: ältestes Datum");
wa.Taken = new DateTime(2019, 7, 13, 10, 0, 0);
Assert(!ItemFilter.NoTaken.Matches(wa) && ItemFilter.Differs.Matches(wa) && ItemFilter.Photos.Matches(wa) && ItemFilter.NoGps.Matches(wa) && !ItemFilter.HasGps.Matches(wa), "Filter");
var u = new UndoRec(wa.Created, wa.Modified, wa.Taken, null, null, true, true).Restore();
Assert(u.Geo.Kind == GeoKind.Remove && u.Plan.Taken == wa.Taken, "Rückgängig: Standort entfernen");
Assert(new UndoRec(wa.Created, wa.Modified, null, 1, 2, false, true).Restore() is { Geo: { Kind: GeoKind.Set, Lat: 1, Lon: 2 } }, "Rückgängig: Standort setzen");
Assert(Similar.Cluster([1UL, 3UL, ulong.MaxValue, null, ulong.MaxValue - 1], 2).Select(g => g.Count).Order().SequenceEqual(new[] { 2, 2 }), "Ähnliche: Cluster");
Assert(Organizer.Date(wa, DateSource.Smart) == wa.Taken, "Smart: Aufnahme");
Assert(Organizer.Format(Pattern.YearDay, D(2024, 7, 13)) == Path.Combine("2024", "2024-07-13") && Organizer.Format(Pattern.Year, D(2024, 7, 13)) == "2024", "Strukturen");
var csv = Csv.Make([new Item { FullPath = Path.Combine(d5, "=SUM(A1);\"q\".txt"), Size = 5 }]);
Assert(csv.StartsWith("﻿Pfad;Name;Art") && csv.Contains("\"'=SUM(A1);\"\"q\"\".txt\"") && csv.Contains(";Datei;5;"), "CSV");
Console.WriteLine("Neue Funktionen ok");
Console.WriteLine("OK");

static void Assert(bool c, string what) { if (!c) { Console.WriteLine("FEHLER: " + what); Environment.Exit(1); } }
