#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rokas.Editor
{
    // Local research entry point only. No scene saving, editor quitting or package changes.
    // TestRunner is reflected because the installed package is not auto-referenced.
    [InitializeOnLoad]
    public static class CombatFidelityValidationRunner
    {
        [Serializable] private sealed class Request
        {
            public string id = null, op = null, testMode = null, outputDirectory = null;
            public string[] testNames = null, groupNames = null;
        }
        [Serializable] private sealed class SceneInfo { public string name, path; public bool isDirty, isLoaded; }
        [Serializable] private sealed class ConsoleEntry { public int mode; public string message; }
        [Serializable] private sealed class Snapshot
        {
            public string unityVersion, projectPath, activeScene, activeScenePath, testApiError;
            public bool isPlaying, isPlayingOrWillChangePlaymode, isCompiling, isUpdating, activeSceneIsDirty, testRunActive;
            public SceneInfo[] scenes;
            public ConsoleEntry[] consoleEntries;
        }
        [Serializable] private sealed class TestCase
        {
            public string fullName, result, message, stackTrace;
            public double duration;
        }
        [Serializable] private sealed class Summary
        {
            public string result; public int passed, failed, skipped, inconclusive; public double duration;
        }
        [Serializable] private sealed class Response
        {
            public Request request;
            public string status, updatedUtc, message, runGuid, currentTest, xmlPath;
            public Snapshot before, after;
            public Summary summary;
            public List<TestCase> tests = new List<TestCase>();
        }
        [Serializable] private sealed class CompileEvent
        {
            public string assembly, utc;
            public string[] errors;
        }

        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string Root = Path.Combine(ProjectRoot, "Library", "CombatFidelityResearch");
        private static readonly string Requests = Path.Combine(Root, "requests");
        private static readonly string Responses = Path.Combine(Root, "responses");
        private static readonly string ActivePath = Path.Combine(Root, "active-run.json");
        private static readonly BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        private static Type apiType;
        private static ScriptableObject api;
        private static object callbacks;
        private static string apiError;
        private static Response active;
        private static double nextPoll;

        static CombatFidelityValidationRunner()
        {
            Directory.CreateDirectory(Requests);
            Directory.CreateDirectory(Responses);
            if (File.Exists(ActivePath))
            {
                try { active = JsonUtility.FromJson<Response>(File.ReadAllText(ActivePath)); }
                catch (Exception e) { apiError = e.Message; }
            }
            InitializeApi();
            EditorApplication.update += Poll;
            CompilationPipeline.assemblyCompilationFinished += CompilationFinished;
            EditorApplication.delayCall += () => WriteJson(Path.Combine(Root, "state.json"),
                new Response { status = "ready", updatedUtc = Utc(), before = GetSnapshot(), message = apiError });
        }

        private static Type FindType(string fullName)
        {
            Type direct = fullName.StartsWith("UnityEditor.TestTools.TestRunner.Api.", StringComparison.Ordinal)
                ? Type.GetType(fullName + ", UnityEditor.TestRunner", false) : null;
            return direct ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false))
                .FirstOrDefault(t => t != null);
        }

        private static void InitializeApi()
        {
            try
            {
                apiType = FindType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi");
                Type contract = FindType("UnityEditor.TestTools.TestRunner.Api.ICallbacks");
                Type errorContract = FindType("UnityEditor.TestTools.TestRunner.Api.IErrorCallbacks");
                if (apiType == null || contract == null) throw new InvalidOperationException("Installed TestRunner API is not loaded.");
                api = ScriptableObject.CreateInstance(apiType);
                api.hideFlags = HideFlags.HideAndDontSave;
                var assembly = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName("RokasCombatResearchCallbacks"), AssemblyBuilderAccess.Run);
                var builder = assembly.DefineDynamicModule("Callbacks").DefineType(
                    "LocalTestCallbacks", TypeAttributes.Public | TypeAttributes.Sealed);
                builder.AddInterfaceImplementation(contract);
                if (errorContract != null) builder.AddInterfaceImplementation(errorContract);
                builder.DefineDefaultConstructor(MethodAttributes.Public);
                MethodInfo handler = typeof(CombatFidelityValidationRunner).GetMethod(nameof(HandleCallback), Public);
                IEnumerable<MethodInfo> methods = contract.GetMethods();
                if (errorContract != null) methods = methods.Concat(errorContract.GetMethods());
                foreach (MethodInfo method in methods.GroupBy(m => m.Name).Select(g => g.First()))
                {
                    Type argument = method.GetParameters()[0].ParameterType;
                    var implementation = builder.DefineMethod(method.Name,
                        MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
                        MethodAttributes.HideBySig | MethodAttributes.NewSlot, typeof(void), new[] { argument });
                    var il = implementation.GetILGenerator();
                    il.Emit(OpCodes.Ldstr, method.Name);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, handler);
                    il.Emit(OpCodes.Ret);
                    builder.DefineMethodOverride(implementation, method);
                }
                callbacks = Activator.CreateInstance(builder.CreateType());
                apiType.GetMethods(Public).First(m => m.Name == "RegisterCallbacks" && m.IsGenericMethodDefinition)
                    .MakeGenericMethod(callbacks.GetType()).Invoke(api, new[] { callbacks, (object)0 });
            }
            catch (Exception e) { apiError = ExceptionMessage(e); }
        }

        private static void Poll()
        {
            // During a test, only callbacks write progress. Avoid polling disk during measured actions.
            if (active != null || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            foreach (string file in Directory.GetFiles(Requests, "*.json").OrderBy(p => p))
            {
                Request request;
                try { request = JsonUtility.FromJson<Request>(File.ReadAllText(file)); }
                catch { continue; } // Sender writes a temporary file and atomically renames it.
                if (request == null || !Regex.IsMatch(request.id ?? "", @"^[A-Za-z0-9_.-]{1,100}$")) continue;
                string responsePath = Path.Combine(Responses, request.id + ".json");
                if (File.Exists(responsePath)) continue; // Unique IDs are never replayed after reload.
                var response = new Response { request = request, before = GetSnapshot(), updatedUtc = Utc() };
                if (request.op == "state")
                {
                    response.status = "ready";
                    WriteJson(responsePath, response);
                    continue;
                }
                if (request.op != "run_tests")
                {
                    Reject(response, "Unsupported operation. Use state or run_tests.");
                    continue;
                }
                if (api == null || !string.IsNullOrEmpty(apiError)) { Reject(response, apiError); continue; }
                if (response.before.isPlayingOrWillChangePlaymode || response.before.isPlaying ||
                    response.before.isCompiling || response.before.isUpdating || response.before.testRunActive)
                {
                    Reject(response, "Editor is playing, changing play state, compiling/updating, or already running tests.");
                    continue;
                }
                if (response.before.scenes.Any(s => s.isDirty))
                {
                    Reject(response, "Loaded scene has unsaved changes. No scene was saved or changed.");
                    continue;
                }
                if (request.testMode != "EditMode" && request.testMode != "PlayMode")
                {
                    Reject(response, "testMode must be EditMode or PlayMode.");
                    continue;
                }
                if (!(request.testNames?.Any(n => !string.IsNullOrWhiteSpace(n)) == true) &&
                    !(request.groupNames?.Any(n => !string.IsNullOrWhiteSpace(n)) == true))
                {
                    Reject(response, "Explicit testNames or groupNames are required; unfiltered global runs are rejected.");
                    continue;
                }
                try
                {
                    string output = string.IsNullOrWhiteSpace(request.outputDirectory) ? Responses :
                        Path.GetFullPath(request.outputDirectory);
                    if (!Within(output, Root) && !Within(output, @"D:\DD2-Research"))
                        throw new InvalidOperationException("Output must stay in research or this helper's Library directory.");
                    Directory.CreateDirectory(output);
                    response.xmlPath = Path.Combine(output, request.id + ".xml");
                    response.status = "scheduled";
                    active = response;
                    Persist();
                    Type filterType = FindType("UnityEditor.TestTools.TestRunner.Api.Filter");
                    object filter = Activator.CreateInstance(filterType);
                    filterType.GetField("testMode").SetValue(filter,
                        Enum.Parse(filterType.GetField("testMode").FieldType, request.testMode));
                    filterType.GetField("testNames").SetValue(filter, request.testNames);
                    filterType.GetField("groupNames").SetValue(filter, request.groupNames);
                    Array filters = Array.CreateInstance(filterType, 1);
                    filters.SetValue(filter, 0);
                    Type settingsType = FindType("UnityEditor.TestTools.TestRunner.Api.ExecutionSettings");
                    object settings = Activator.CreateInstance(settingsType, new object[] { filters });
                    active.runGuid = (string)apiType.GetMethod("Execute", Public).Invoke(api, new[] { settings });
                    Persist();
                }
                catch (Exception e) { FinishError(ExceptionMessage(e)); }
                break;
            }
        }


        private static bool Within(string path, string root)
        {
            string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string allowed = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalized.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void Reject(Response response, string reason)
        {
            response.status = "blocked";
            response.message = reason;
            WriteJson(Path.Combine(Responses, response.request.id + ".json"), response);
        }

        private static Snapshot GetSnapshot()
        {
            Scene scene = SceneManager.GetActiveScene();
            var scenes = new List<SceneInfo>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                scenes.Add(new SceneInfo { name = loaded.name, path = loaded.path,
                    isDirty = loaded.isDirty, isLoaded = loaded.isLoaded });
            }
            bool running = false;
            try
            {
                MethodInfo method = apiType?.GetMethod("IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
                running = method != null && (bool)method.Invoke(null, null);
            }
            catch { }
            return new Snapshot { unityVersion = Application.unityVersion, projectPath = ProjectRoot,
                isPlaying = EditorApplication.isPlaying, isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode,
                isCompiling = EditorApplication.isCompiling, isUpdating = EditorApplication.isUpdating,
                activeScene = scene.name, activeScenePath = scene.path, activeSceneIsDirty = scene.isDirty,
                scenes = scenes.ToArray(), testRunActive = running, testApiError = apiError,
                consoleEntries = ReadConsole() };
        }

        private static ConsoleEntry[] ReadConsole()
        {
            var entries = new List<ConsoleEntry>();
            Type logs = FindType("UnityEditor.LogEntries"), entryType = FindType("UnityEditor.LogEntry");
            if (logs == null || entryType == null) return entries.ToArray();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            bool started = false;
            try
            {
                logs.GetMethod("StartGettingEntries", flags)?.Invoke(null, null);
                started = true;
                int count = (int)logs.GetMethod("GetCount", flags).Invoke(null, null);
                object entry = Activator.CreateInstance(entryType);
                MethodInfo read = logs.GetMethod("GetEntryInternal", flags);
                FieldInfo message = entryType.GetField("message", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo mode = entryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = Mathf.Max(0, count - 100); i < count; i++)
                {
                    read.Invoke(null, new[] { (object)i, entry });
                    entries.Add(new ConsoleEntry { mode = mode == null ? 0 : (int)mode.GetValue(entry),
                        message = Sanitize(message?.GetValue(entry)?.ToString()) });
                }
            }
            catch (Exception e) { entries.Add(new ConsoleEntry { message = "Console reflection unavailable: " + ExceptionMessage(e) }); }
            finally
            {
                if (started) try { logs.GetMethod("EndGettingEntries", flags)?.Invoke(null, null); } catch { }
            }
            return entries.ToArray();
        }

        private static void CompilationFinished(string assembly, CompilerMessage[] messages)
        {
            string[] errors = messages.Where(m => m.type == CompilerMessageType.Error)
                .Select(m => Sanitize(m.file + ":" + m.line + " " + m.message)).ToArray();
            if (errors.Length == 0) return;
            WriteJson(Path.Combine(Root, "last-compilation-errors.json"),
                new CompileEvent { assembly = assembly, utc = Utc(), errors = errors });
        }

        // Called by the dynamically generated adapter; only the explicitly scheduled run is recorded.
        public static void HandleCallback(string kind, object value)
        {
            if (active == null) return;
            try
            {
                if (kind == "OnError") { FinishError(value?.ToString()); return; }
                if (kind == "RunStarted") { active.status = "running"; Persist(); return; }
                if (kind == "TestStarted") { active.currentTest = Text(value, "FullName"); Persist(); return; }
                if (kind == "TestFinished")
                {
                    object test = Property(value, "Test");
                    if (!(bool)(Property(test, "IsSuite") ?? true))
                        active.tests.Add(new TestCase { fullName = Text(value, "FullName"), result = Text(value, "ResultState"),
                            message = Sanitize(Text(value, "Message")), stackTrace = Sanitize(Text(value, "StackTrace")),
                            duration = Convert.ToDouble(Property(value, "Duration") ?? 0d) });
                    Persist();
                    return;
                }
                if (kind != "RunFinished") return;
                apiType.GetMethod("SaveResultToFile", Public).Invoke(null, new[] { value, (object)active.xmlPath });
                active.summary = new Summary { result = Text(value, "ResultState"),
                    passed = Convert.ToInt32(Property(value, "PassCount")), failed = Convert.ToInt32(Property(value, "FailCount")),
                    skipped = Convert.ToInt32(Property(value, "SkipCount")), inconclusive = Convert.ToInt32(Property(value, "InconclusiveCount")),
                    duration = Convert.ToDouble(Property(value, "Duration")) };
                active.status = "finished";
                active.currentTest = null;
                active.after = GetSnapshot();
                Persist();
                string output = Path.ChangeExtension(active.xmlPath, ".json");
                WriteJson(output, active);
                File.Delete(ActivePath);
                active = null;
            }
            catch (Exception e) { FinishError(ExceptionMessage(e)); }
        }

        private static object Property(object obj, string property) => obj?.GetType().GetProperty(property, Public)?.GetValue(obj);
        private static string Text(object obj, string property) => Property(obj, property)?.ToString();
        private static string Utc() => DateTime.UtcNow.ToString("O");
        private static string ExceptionMessage(Exception e) => (e is TargetInvocationException && e.InnerException != null ? e.InnerException : e).ToString();
        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string redacted = Regex.Replace(value, @"(?i)(accessToken|authorization|bearer)([\s:=]+)[^\s""']+", "$1$2[REDACTED]");
            return redacted.Length > 12000 ? redacted.Substring(0, 12000) : redacted;
        }
        private static void FinishError(string message)
        {
            if (active == null) return;
            active.status = "error";
            active.message = Sanitize(message);
            active.after = GetSnapshot();
            Persist();
            File.Delete(ActivePath);
            active = null;
        }
        private static void Persist()
        {
            active.updatedUtc = Utc();
            WriteJson(ActivePath, active);
            WriteJson(Path.Combine(Responses, active.request.id + ".json"), active);
        }
        private static void WriteJson(string file, object data)
        {
            string temp = file + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(file)) File.Delete(file);
            File.Move(temp, file);
        }
    }
}
#endif
