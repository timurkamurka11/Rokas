using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rokas.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Yarn.Unity;

namespace Rokas.Core.Tests
{
    public sealed class MessagesYarnTests
    {
        private const string ProjectPath = "Assets/Rokas/Resources/Messages/Dialogue/ROKASMessages.yarnproject";

        [Test]
        public void KaitoProjectImportsAndContainsStartNode()
        {
            YarnProject project = AssetDatabase.LoadAssetAtPath<YarnProject>(ProjectPath);
            Assert.That(project, Is.Not.Null, "ROKAS Messages Yarn project must import as YarnProject");
            CollectionAssert.Contains(project.NodeNames, "Kaito_Start", "Kaito start node must exist in compiled Yarn program");
            Assert.That(project.InitialValues.ContainsKey("$kaitoApproach"), Is.True, "Kaito dialogue must declare persistent branch variable");
        }

        [UnityTest]
        public IEnumerator KaitoDetailsBranchEmitsChoicesAndCoordinates()
        {
            return YarnTask.ToCoroutine(async () =>
            {
                YarnProject project = RequireProject();
                GameObject host = new GameObject("Messages Yarn Details Test");
                try
                {
                    DialogueRunner runner = host.AddComponent<DialogueRunner>();
                    ProbePresenter presenter = host.AddComponent<ProbePresenter>();
                    presenter.SelectionPlan.Enqueue(0);
                    presenter.SelectionPlan.Enqueue(1);
                    runner.DialoguePresenters = new[] { presenter };
                    runner.SetProject(project);

                    await runner.StartDialogue("Kaito_Start");

                    Assert.That(presenter.Lines, Does.Contain("Нужно обсудить то, что я видел сегодня ночью."));
                    Assert.That(presenter.Lines, Does.Contain("Ты правильно спрашиваешь. Я видел след, которого там быть не должно."), "details branch must continue through its authored response");
                    Assert.That(presenter.Lines, Does.Contain("Похоже, в восточном районе появилась нестабильная аномалия. Это не обычный ёкай."));
                    Assert.That(presenter.Lines, Does.Contain("Я отправляю тебе координаты. Будь осторожен."));
                    Assert.That(presenter.OptionSets.Count, Is.EqualTo(2), "Kaito flow must contain two meaningful choice points");
                    Assert.That(presenter.OptionSets[0].Count, Is.InRange(2, 4));
                    Assert.That(presenter.OptionSets[1].Count, Is.InRange(2, 4));
                    Assert.That(presenter.OptionSets[0], Does.Contain("Что именно ты видел?"));
                    Assert.That(presenter.OptionSets[1], Does.Contain("Сначала проверю сведения Гильдии."));
                    Assert.That(runner.VariableStorage.TryGetValue<string>("$kaitoApproach", out string approach), Is.True);
                    Assert.That(approach, Is.EqualTo("details"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            });
        }

        [UnityTest]
        public IEnumerator KaitoSkepticBranchContinuesDifferently()
        {
            return YarnTask.ToCoroutine(async () =>
            {
                YarnProject project = RequireProject();
                GameObject host = new GameObject("Messages Yarn Skeptic Test");
                try
                {
                    DialogueRunner runner = host.AddComponent<DialogueRunner>();
                    ProbePresenter presenter = host.AddComponent<ProbePresenter>();
                    presenter.SelectionPlan.Enqueue(1);
                    presenter.SelectionPlan.Enqueue(0);
                    runner.DialoguePresenters = new[] { presenter };
                    runner.SetProject(project);

                    await runner.StartDialogue("Kaito_Start");

                    Assert.That(presenter.Lines, Does.Contain("Я проверил дважды. Обычный ёкай не оставляет такой след."), "skeptic branch must produce distinct authored continuation");
                    Assert.That(runner.VariableStorage.TryGetValue<string>("$kaitoApproach", out string approach), Is.True);
                    Assert.That(approach, Is.EqualTo("skeptic"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            });
        }

        [Test]
        public void ProductionYarnAdapterExistsAndRejectsMissingNodeVisibly()
        {
            Type presenterType = typeof(Rokas.Presentation.LaptopView).Assembly.GetType("Rokas.Presentation.MessagesDialoguePresenter");
            Type controllerType = typeof(Rokas.Presentation.LaptopView).Assembly.GetType("Rokas.Presentation.MessagesYarnController");
            Assert.That(presenterType, Is.Not.Null, "custom ROKAS Yarn presenter must exist");
            Assert.That(controllerType, Is.Not.Null, "ROKAS Messages Yarn controller must exist");
            Assert.That(controllerType.GetProperty("LastError"), Is.Not.Null, "missing Yarn nodes must expose visible diagnostic state");
            Assert.That(controllerType.GetMethod("StartDialogue"), Is.Not.Null, "controller must own validated dialogue startup");
        }

        private static YarnProject RequireProject()
        {
            YarnProject project = AssetDatabase.LoadAssetAtPath<YarnProject>(ProjectPath);
            Assert.That(project, Is.Not.Null, "ROKAS Messages Yarn project must import before flow tests can run");
            return project;
        }

        private sealed class ProbePresenter : DialoguePresenterBase
        {
            public readonly List<string> Lines = new List<string>();
            public readonly List<List<string>> OptionSets = new List<List<string>>();
            public readonly Queue<int> SelectionPlan = new Queue<int>();

            public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
            {
                Lines.Add(line.TextWithoutCharacterName.Text.Trim());
                return YarnTask.CompletedTask;
            }

            public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken token)
            {
                OptionSets.Add(dialogueOptions.Select(option => option.Line.TextWithoutCharacterName.Text.Trim()).ToList());
                if (SelectionPlan.Count == 0)
                {
                    throw new InvalidOperationException("Test selection plan exhausted before Yarn options completed.");
                }
                int index = SelectionPlan.Dequeue();
                if (index < 0 || index >= dialogueOptions.Length)
                {
                    throw new InvalidOperationException("Test selected invalid Yarn option index " + index + ".");
                }
                return YarnTask<DialogueOption?>.FromResult(dialogueOptions[index]);
            }

            public override YarnTask OnDialogueStartedAsync()
            {
                return YarnTask.CompletedTask;
            }

            public override YarnTask OnDialogueCompleteAsync()
            {
                return YarnTask.CompletedTask;
            }
        }
    }
}
