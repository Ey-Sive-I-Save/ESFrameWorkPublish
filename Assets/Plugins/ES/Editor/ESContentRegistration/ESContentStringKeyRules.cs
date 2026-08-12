using System;
using System.Text;

namespace ES
{
    public static class ESContentStringKeyRules
    {
        public const int MaxLength = 256;

        public static bool TryValidateStableKey(
            ESContentStableKeyMode requestedMode,
            int enumKey,
            string stringKey,
            out ESContentStableKeyMode resolvedMode,
            out string error)
        {
            resolvedMode = requestedMode == ESContentStableKeyMode.Auto
                ? InferMode(enumKey, stringKey)
                : requestedMode;

            if (enumKey < 0 || enumKey > ushort.MaxValue)
            {
                error = "EnumKey 必须位于 0..65535。";
                return false;
            }

            bool hasEnum = enumKey != 0;
            bool hasString = !string.IsNullOrEmpty(stringKey);
            switch (resolvedMode)
            {
                case ESContentStableKeyMode.StringOnly:
                    if (hasEnum || !hasString)
                    {
                        error = "StringOnly 必须使用 enumKey=0 且提供非空 StringKey。";
                        return false;
                    }
                    break;
                case ESContentStableKeyMode.EnumOnly:
                    if (!hasEnum || hasString)
                    {
                        error = "EnumOnly 必须提供非零 EnumKey 且 StringKey 为空。";
                        return false;
                    }
                    break;
                case ESContentStableKeyMode.DualAlias:
                    if (!hasEnum || !hasString)
                    {
                        error = "DualAlias 必须同时提供非零 EnumKey 与 StringKey。";
                        return false;
                    }
                    break;
                default:
                    error = "必须提供 EnumKey、StringKey 或二者的正式别名组合。";
                    return false;
            }

            if (hasString && !TryValidateStringKey(stringKey, out error))
                return false;

            error = string.Empty;
            return true;
        }

        public static bool TryValidateStringKey(string key, out string error)
        {
            if (string.IsNullOrEmpty(key))
            {
                error = "StringKey 不能为空。";
                return false;
            }
            if (key.Length > MaxLength)
            {
                error = "StringKey 长度不能超过 " + MaxLength + "。";
                return false;
            }
            if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
            {
                error = "StringKey 不能包含首尾空白；系统不会自动 Trim。";
                return false;
            }
            for (int i = 0; i < key.Length; i++)
            {
                if (char.IsControl(key[i]))
                {
                    error = "StringKey 不能包含控制字符。";
                    return false;
                }
            }
            if (!key.IsNormalized(NormalizationForm.FormC))
            {
                error = "StringKey 必须使用 Unicode NFC（Normalization Form C）。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryValidateRequestId(string requestId, out string error)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                error = "commit=true 时必须提供 requestId。";
                return false;
            }
            if (requestId.Length > 128 || !string.Equals(requestId, requestId.Trim(), StringComparison.Ordinal))
            {
                error = "requestId 必须是长度不超过 128 的无首尾空白字符串。";
                return false;
            }
            for (int i = 0; i < requestId.Length; i++)
            {
                if (char.IsControl(requestId[i]))
                {
                    error = "requestId 不能包含控制字符。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static ESContentStableKeyMode InferMode(int enumKey, string stringKey)
        {
            if (enumKey != 0 && !string.IsNullOrEmpty(stringKey))
                return ESContentStableKeyMode.DualAlias;
            if (enumKey != 0)
                return ESContentStableKeyMode.EnumOnly;
            if (!string.IsNullOrEmpty(stringKey))
                return ESContentStableKeyMode.StringOnly;
            return ESContentStableKeyMode.Auto;
        }
    }
}
