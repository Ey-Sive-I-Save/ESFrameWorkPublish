using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESAutomationAiBridgeTests
    {
        [Test]
        public void ExecuteJson_RejectsUnknownEnvelopeField()
        {
            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': '0123456789abcdef0123456789abcdef',
                'actorId': 'codex.local',
                'action': 'listTasks',
                'payload': {},
                'unexpected': true
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Rejected"));
            Assert.That(response.message, Does.Contain("未注册 unexpected"));
        }

        [Test]
        public void ExecuteJson_RejectsArbitraryContentType()
        {
            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': 'fedcba9876543210fedcba9876543210',
                'actorId': 'codex.local',
                'action': 'submitContentProposal',
                'payload': {
                    'contentType': 'es.unregistered.content',
                    'contentVersion': 1,
                    'schemaHash': '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef',
                    'payload': {}
                }
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Rejected"));
            Assert.That(response.message, Does.Contain("未注册内容类型"));
        }

        [Test]
        public void ExecuteJson_ListTasks_UsesStructuredResponse()
        {
            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': '00112233445566778899aabbccddeeff',
                'actorId': 'codex.local',
                'action': 'listTasks',
                'payload': {}
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Completed"));
            Assert.That(responseJson, Does.Contain("\"tasks\":"));
            Assert.That(responseJson, Does.Contain("\"contentTypes\":"));
        }

        [Test]
        public void SceneScan_ExplicitlyDisallowsPlayMode()
        {
            RuntimeHelpers.RunClassConstructor(typeof(ESAutomationSceneScanPrototype).TypeHandle);

            ESAutomationTaskDescriptor sceneScan = FindSceneScanDescriptor();

            Assert.That(sceneScan, Is.Not.Null);
            Assert.That(sceneScan.allowAiInvoke, Is.True);
            Assert.That(sceneScan.allowInPlayMode, Is.False);
        }

        [Test]
        public void SceneScan_ExposesInteractivePresetAndTypedInputSchema()
        {
            RuntimeHelpers.RunClassConstructor(typeof(ESAutomationSceneScanPrototype).TypeHandle);
            ESAutomationTaskDescriptor sceneScan = FindSceneScanDescriptor();

            Assert.That(sceneScan, Is.Not.Null);
            ESAutomationTaskPresetDescriptor interactivePreset = null;
            foreach (ESAutomationTaskPresetDescriptor preset in sceneScan.presets)
            {
                if (preset != null && preset.presetId == "interactive")
                {
                    interactivePreset = preset;
                    break;
                }
            }
            Assert.That(interactivePreset, Is.Not.Null);
            Assert.That(sceneScan.TryGetInputSchema("scene-scan.report-options", sceneScan.inputSchemaHash, out ESAutomationInputSchemaDescriptor schema), Is.True);
            Assert.That(schema.fields.Count, Is.EqualTo(3));
            Assert.That(schema.fields[0].fieldId, Is.EqualTo("includeInactive"));
            Assert.That(schema.fields[0].valueType, Is.EqualTo("Boolean"));
            Assert.That(schema.fields[1].fieldId, Is.EqualTo("detailMode"));
            Assert.That(schema.fields[1].valueType, Is.EqualTo("Choice"));
            Assert.That(schema.fields[2].fieldId, Is.EqualTo("topComponentCount"));
            Assert.That(schema.fields[2].valueType, Is.EqualTo("Integer"));
            Assert.That(schema.fields[2].minimumInteger, Is.EqualTo(1));
            Assert.That(schema.fields[2].maximumInteger, Is.EqualTo(50));
        }

        private static ESAutomationTaskDescriptor FindSceneScanDescriptor()
        {
            foreach (ESAutomationTaskDescriptor descriptor in ESAutomationFacade.CopyDescriptors())
            {
                if (descriptor.taskId == "es.scene.scan" && descriptor.taskVersion == 1)
                    return descriptor;
            }
            return null;
        }

        [Serializable]
        private sealed class ESAutomationResponseSummary
        {
            public string status;
            public string message;
        }
    }
}
