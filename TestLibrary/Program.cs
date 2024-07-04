// See https://aka.ms/new-console-template for more information
using GDJLibrary;

Console.WriteLine("Hello, World!");
GCommon.Info("Info");
GCommon.Debug("Debug");
GCommon.Error("Error");
string csvFileName = Path.Combine(GCommon.CurPath, "TestFiles", "TESTCSV.csv");


using(DBInfoMsSql sql = new DBInfoMsSql(@"Password=shinseiDX2022;Persist Security Info=True;User ID=ccflowtest;Initial Catalog=ccflow8_test_0529;Data Source=ccflowtest.database.windows.net;Timeout=999;MultipleActiveResultSets=true;Pooling=true;Max Pool Size=500"))
{
    await sql.ReadDBInfo();
    sql.LoopTableRowsForKey("MT_Auth", new Dictionary<string, object>() { { "SHOZOKUCODE", "00004" }, { "SHONINSHAKBN", "UW002" }, { "TEKIYOYMD_FROM", "20090527" } }, (row) =>
    {
        GCommon.Debug(GCommon.GetObjectJsonString(row));
        return true;
    });
}

bool bl = GCommon.CheckJpnString("ありがとABCDうござＢＣＤいます。张爱は最近近爱藤さんどうですか", out string errorStr);
if(!bl)
{
    GCommon.Error(errorStr);
}
// 读取CSV操作
var csvs = await GCommon.ReadCsv(csvFileName);

GCommon.ReadCsv(csvFileName, (row) =>
{
    int n = 0;
    foreach (var c in row)
    {
        GCommon.Info($"{c.Key}, {c.Value}");
        if (n++ > 100)
        {
            return false;
        }
    }
    return true;
});

GCommon.LoadEmbedded("TestLibrary.TestFiles.Embedded.txt", (stream) => {
    using(StreamReader sr = new StreamReader(stream))
    {
        GCommon.Debug(sr.ReadToEnd());
    }

});

GCommon.Info("Info");
GCommon.Debug("Debug");
GCommon.Error("Error");