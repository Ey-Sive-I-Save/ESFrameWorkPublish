namespace ES
{
    public static class ESGameSave
    {
        public static ESGameSaveModule Module
        {
            get { return ESGameManager.GetModuleFast<ESGameSaveModule>(); }
        }

        public static void Set<T>(string sectionKey, T data)
        {
            ESGameSaveModule module = Module;
            if (module != null)
                module.Set(sectionKey, data);
        }

        public static void Set<T>(string slotId, string sectionKey, T data)
        {
            ESGameSaveModule module = Module;
            if (module != null)
                module.Set(slotId, sectionKey, data);
        }

        public static void Set<T>(string slotId, string displayName, string sectionKey, T data)
        {
            ESGameSaveModule module = Module;
            if (module != null)
                module.Set(slotId, displayName, sectionKey, data);
        }

        public static bool Get<T>(string sectionKey, out T value)
        {
            ESGameSaveModule module = Module;
            if (module != null)
                return module.Get(sectionKey, out value);

            value = default;
            return false;
        }

        public static bool Get<T>(string slotId, string sectionKey, out T value)
        {
            ESGameSaveModule module = Module;
            if (module != null)
                return module.Get(slotId, sectionKey, out value);

            value = default;
            return false;
        }

        public static bool Save()
        {
            ESGameSaveModule module = Module;
            return module != null && module.Save();
        }

        public static bool Save(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null && module.Save(slotId);
        }

        public static bool Load()
        {
            ESGameSaveModule module = Module;
            return module != null && module.Load();
        }

        public static bool Load(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null && module.Load(slotId);
        }

        public static bool Has()
        {
            ESGameSaveModule module = Module;
            return module != null && module.Has();
        }

        public static bool Has(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null && module.Has(slotId);
        }

        public static bool Delete()
        {
            ESGameSaveModule module = Module;
            return module != null && module.Delete();
        }

        public static bool Delete(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null && module.Delete(slotId);
        }

        public static ESGameSaveSlotInfo Info()
        {
            ESGameSaveModule module = Module;
            return module != null ? module.Info() : null;
        }

        public static ESGameSaveSlotInfo Info(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null ? module.Info(slotId) : null;
        }
    }
}
