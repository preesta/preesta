using System.Collections.Generic;
using Messaging;
using JiraRest;

namespace Preesta.Data.Supplying.Convert
{
    internal interface IPackageConverter<TIssueType> 
    {
        Message[] ToMessages(IEnumerable<Package<SendsNotification, TIssueType>> packages);
        HttpRequest[] ToHttpRequests(IEnumerable<Package<SelfUpdate, TIssueType>> packages);
    }
}
