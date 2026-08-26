using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ES
{
    internal static class ESAssetDeliveryModeEditorUtility
    {
        public static ESAssetDeliveryMode ResolveLibrary(string libraryFolder)
        {
            string normalized = ESAssetPipelineIO.SafeSegment(libraryFolder);
            ESAssetLibrary library = ESEditorSO.GetGroupOfType<ESAssetLibrary>()
                ?.FirstOrDefault(item => item != null && string.Equals(ESAssetPipelineIO.SafeSegment(item.LibFolderName), normalized, StringComparison.Ordinal));
            if (library != null)
            {
                bool migrated = !library.HasExplicitDeliveryMode;
                if (migrated)
                    Undo.RecordObject(library, "迁移资产库分发方式");
                library.EnsureDeliveryModeMigrated();
                if (migrated)
                    EditorUtility.SetDirty(library);
                return library.DeliveryMode;
            }

            foreach (ESAssetLibraryConsumer consumer in ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>() ?? Enumerable.Empty<ESAssetLibraryConsumer>())
            {
                if (consumer == null || !string.Equals(ESAssetPipelineIO.GameCoreLibraryFolder(consumer.ConsumerId), normalized, StringComparison.Ordinal))
                    continue;
                return consumer.GameCoreDeliveryMode;
            }

            throw new InvalidOperationException("无法为 Library 解析分发方式：" + libraryFolder);
        }

        public static bool IsValid(ESAssetDeliveryMode mode)
        {
            return mode == ESAssetDeliveryMode.BuiltIn || mode == ESAssetDeliveryMode.Updateable || mode == ESAssetDeliveryMode.Remote;
        }
    }
}
