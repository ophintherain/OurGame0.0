namespace Spotlight.Exploration
{
    /// <summary>可探索点的类型。</summary>
    public enum ExplorationPointType
    {
        /// <summary>获得武器或物品，会写入背包。</summary>
        Item,

        /// <summary>触发剧情，本期只把剧情标识通过事件发出去。</summary>
        Story
    }
}
