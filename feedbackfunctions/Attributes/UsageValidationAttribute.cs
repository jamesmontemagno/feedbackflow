using System;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class UsageValidationAttribute : Attribute
    {
        public UsageType UsageType { get; set; }
        public bool Required { get; set; } = true;
    }
}
