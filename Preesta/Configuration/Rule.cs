using System.Linq;
using Preesta.Configuration.Action;

namespace Preesta.Configuration
{
    public abstract class Rule
    {
        public string? AdditionalPredicateName { get; set; }
        public NotificationSpec? Notification { get; set; }
        public SelfUpdateSpec[] Updates { get; set; } = new SelfUpdateSpec[] {};
   }
}