namespace Preesta.Data.Supplying
{
    /// <summary>
    /// Prepared data for reaction: either to notify via email or to do REST request itself or so.
    /// </summary>
    /// <typeparam name="TItemType">Items: either Release or Jira Issue</typeparam>
    /// <typeparam name="TReaction">Reaction: send notification or update the issue itself</typeparam>
    internal class Package<TReaction, TItemType> : PackageBase where TReaction: new()
    {
        public TReaction Reaction { get; set; } = new TReaction();
        public TItemType[] Items { get; set; } = new TItemType[]{};
    }
}
