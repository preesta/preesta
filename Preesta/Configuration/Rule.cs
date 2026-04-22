using System.Linq;
using Preesta.Configuration.Action;

namespace Preesta.Configuration
{
    public abstract class Rule
    {
        public string? AdditionalPredicateName { get; set; }
        public Notify? HowToNotify { get; set; }
        public Update[] HowToUpdate { get; set; } = new Update[] {};
   }
}