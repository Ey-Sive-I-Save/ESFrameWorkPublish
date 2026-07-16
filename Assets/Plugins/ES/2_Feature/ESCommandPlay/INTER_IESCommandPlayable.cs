namespace ES
{
    public interface IESCommandPlayable
    {
        void OnPlayStart(ESCommandPlayer player);
        ESRunState TickPlay(ESCommandPlayer player, ref ESCommandPlayFrame frame);
        void OnPlayCancel(ESCommandPlayer player);
    }
}
