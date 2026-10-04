using System;
using System.IO;
using System.Linq;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed++;
    }

    private static void Main(string[] args)
    {
        string root = args.Length == 1 ? args[0] : Directory.GetCurrentDirectory();
        string source = Path.Combine(root, "DataSheets", "Balance");
        var current = BalanceCsvDocument.Read(source);
        Check(current.ScalingEnabled && current.HpIncrement == 0.55f, "stage defaults");
        Check(current.Experience.Sum() == 2540 && current.Experience.Length == 9, "experience defaults");
        Check(current.Gold["NormalRoomGold"] == 120 && current.Gold["LargeRoomGold"] == 180, "room defaults");
        Check(current.GenerateGoldCode() == File.ReadAllText(Path.Combine(root,
            "Assets/_Project/Runtime/Core/Scaling/BalanceGoldValues.g.cs")).Replace("\r\n", "\n"), "generated code reproducibility");
        foreach (string key in BalanceCsvDocument.GoldKeys)
            Check((int)typeof(BalanceGoldValues).GetField(key).GetRawConstantValue() == current.Gold[key], key);

        string temp = Path.Combine(Path.GetTempPath(), "capstone-balance-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            void Reset() { foreach (string file in Directory.GetFiles(source, "*.csv")) File.Copy(file, Path.Combine(temp, Path.GetFileName(file)), true); }
            void Reject(string file, string text, string name)
            {
                Reset(); File.WriteAllText(Path.Combine(temp, file), text);
                try { BalanceCsvDocument.Read(temp); }
                catch (FormatException) { passed++; return; }
                throw new Exception("Accepted invalid input: " + name);
            }
            Reject("StageHealth.csv", "key,value\nenabled,1\nhpMultiplierPerClearedStage,NaN\n", "NaN");
            Reject("StageHealth.csv", "key,value\nenabled,2\nhpMultiplierPerClearedStage,0.5\n", "invalid bool");
            Reject("StageHealth.csv", "key,value\nenabled,1\nenabled,0\n", "duplicate key");
            Reject("StageHealth.csv", "key,value\nenabled,1\nhpMultiplierPerClearedStage,-1\n", "negative HP");
            Reject("StageHealth.csv", "key,value\nenabled,1\nhpMultiplierPerClearedStage,\"0.5\n", "unclosed quote");
            Reject("LevelExperience.csv", "fromLevel,requiredExperience\n1,100\n3,200\n", "missing level");
            Reject("LevelExperience.csv", "fromLevel,requiredExperience\n1,100\n1,200\n", "duplicate level");
            Reject("LevelExperience.csv", "fromLevel,requiredExperience\n1,0\n", "zero EXP");
            Reject("LevelExperience.csv", "fromLevel,requiredExperience\n1,2147483648\n", "integer overflow");
            string gold = File.ReadAllText(Path.Combine(source, "GoldEconomy.csv"));
            Reject("GoldEconomy.csv", gold.Replace("\"WeaponPriceMin\",\"1150\"", "\"WeaponPriceMin\",\"1300\""), "reversed price range");
            Reject("GoldEconomy.csv", gold + "Unexpected,1\n", "unknown key");
            Reset();
            File.WriteAllText(Path.Combine(temp, "LevelExperience.csv"), "fromLevel,requiredExperience\n2,150\n1,90\n");
            Check(BalanceCsvDocument.Read(temp).Experience.SequenceEqual(new[] { 90, 150 }), "level key ordering");
            Reset();
            File.WriteAllText(Path.Combine(temp, "StageHealth.csv"), "\uFEFF\"key\",\"value\"\r\n\"enabled\",\"0\"\r\n\"hpMultiplierPerClearedStage\",\"0.25\"\r\n");
            Check(!BalanceCsvDocument.Read(temp).ScalingEnabled && BalanceCsvDocument.Read(temp).HpIncrement == 0.25f, "BOM / quotes / edited values");
        }
        finally { Directory.Delete(temp, true); }
        Console.WriteLine($"PASS: {passed} balance CSV checks. Unity import/Undo/Play Mode are not covered.");
    }
}
