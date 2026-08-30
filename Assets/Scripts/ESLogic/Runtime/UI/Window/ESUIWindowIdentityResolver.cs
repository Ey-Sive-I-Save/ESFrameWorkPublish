using System;

namespace ES
{
    /// <summary>
    /// Resolves any authored UI alias to the one canonical runtime identity owned by its
    /// catalog definition. This keeps navigation, context and tracing keys stable while
    /// preserving BuiltInId/StringKey compatibility at the public entry points.
    /// </summary>
    public static class ESUIWindowIdentityResolver
    {
        public static bool TryResolve(
            ESUIWindowCatalog catalog,
            ESUIWindowIdentity input,
            out ESUICanonicalId canonicalId,
            out ESUIWindowDefinition definition,
            out string error)
        {
            canonicalId = default;
            definition = null;

            if (catalog == null)
            {
                error = "UI Window Catalog 不能为空。";
                return false;
            }

            if (!input.HasBuiltInId && !input.HasStringKey)
            {
                error = "UI Window Identity 必须包含 BuiltInId 或 StringKey。";
                return false;
            }

            if (!catalog.TryGet(input, out definition) || definition == null)
            {
                error = "UI Window Identity 未能解析到唯一 Definition：" + input;
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.StringKey))
            {
                error = "UI Window Definition 缺少稳定 StringKey：" + definition.name;
                definition = null;
                return false;
            }

            canonicalId = new ESUICanonicalId(definition.StringKey);
            error = null;
            return true;
        }
    }
}
