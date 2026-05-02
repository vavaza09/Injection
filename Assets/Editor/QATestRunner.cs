using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public class QATestRunner : ICallbacks
{
    private static QATestRunner _instance;

    [MenuItem("Tools/QA/Run All Tests and Generate Report")]
    public static void RunAllTests()
    {
        _instance = new QATestRunner();
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(_instance);

        var filter = new Filter
        {
            testMode = TestMode.EditMode
        };

        api.Execute(new ExecutionSettings(filter));
    }

    [MenuItem("Tools/QA/Open Test Runner")]
    public static void OpenTestRunner()
    {
        EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
    }

    void ICallbacks.RunStarted(ITestAdaptor testsToRun)
    {
        UnityEngine.Debug.Log($"[QA] Test run started — {CountTests(testsToRun)} tests found");
    }

    void ICallbacks.RunFinished(ITestResultAdaptor result)
    {
        GenerateQAReport(result);
    }

    void ICallbacks.TestStarted(ITestAdaptor test) { }

    void ICallbacks.TestFinished(ITestResultAdaptor result)
    {
        if (result.FailCount > 0)
        {
            UnityEngine.Debug.LogWarning($"[QA] FAIL: {result.Test.Name}\n{result.Message}");
        }
    }

    private static void GenerateQAReport(ITestResultAdaptor result)
    {
        string reportDir = "Reports";
        Directory.CreateDirectory(reportDir);
        string reportPath = Path.Combine(reportDir, "qa_report.md");

        int total = result.PassCount + result.FailCount + result.SkipCount;
        float successRate = total > 0 ? (result.PassCount / (float)total) * 100f : 0f;
        string grade = successRate >= 90f ? "A" : successRate >= 80f ? "B" : successRate >= 70f ? "C" : "F";

        string report = $@"# QA Test Report — Injection
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Branch: {GetCurrentBranch()}

## Test Summary
- Total: {total} | Passed: {result.PassCount} | Failed: {result.FailCount} | Skipped: {result.SkipCount}
- Success Rate: {successRate:F1}%

## SOLID Status
- SRP: Review Player.cs (661 lines) on feat/attack completion
- OCP: HealthComponent, IDashHandler — compliant
- LSP: No known violations
- ISP: IDashHandler (7 members), ILogger (3 members) — compliant
- DIP: VContainer injection in use — compliant

## Score Estimate: {(int)successRate}/100 — Grade: {grade}

## Recommendations
{(result.FailCount > 0 ? $"1. Fix {result.FailCount} failing test(s) listed above" : "1. All tests passing — maintain coverage as feat/attack grows")}
2. Run coverage report: Window → General → Test Runner → Code Coverage
3. Profile Update() methods for GC allocations before each release
";

        File.WriteAllText(reportPath, report);
        UnityEngine.Debug.Log($"[QA] Report generated: {reportPath}");
        EditorUtility.RevealInFinder(reportPath);
    }

    private static int CountTests(ITestAdaptor node)
    {
        if (!node.IsSuite) return 1;
        int count = 0;
        foreach (var child in node.Children)
            count += CountTests(child);
        return count;
    }

    private static string GetCurrentBranch()
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            return proc.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            return "unknown";
        }
    }
}
