namespace ES
{
    public struct ESCommandPlayFrame
    {
        public float time;
        public float deltaTime;
        public bool cancelRequested;

        public ESCommandPlayFrame(float time, float deltaTime, bool cancelRequested)
        {
            this.time = time;
            this.deltaTime = deltaTime;
            this.cancelRequested = cancelRequested;
        }
    }
}
