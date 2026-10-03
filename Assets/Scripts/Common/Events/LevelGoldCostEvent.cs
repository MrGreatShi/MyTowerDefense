public class LevelGoldCostEvent
{
    public int previousGold;
    public int currentGold;
    public LevelGoldCostEvent(int previousGold, int currentGold)
    {
        this.previousGold = previousGold;
        this.currentGold = currentGold;
    }
}
