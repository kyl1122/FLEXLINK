using System.Collections.Generic;
using FLEXLINK.Models;

namespace FLEXLINK.ViewModels
{
    public class StaffApprovalViewModel
    {
        public List<RegistrationRequest> PendingRegistrations { get; set; } = new List<RegistrationRequest>();
        public List<UserMembership> PendingPayments { get; set; } = new List<UserMembership>();
    }
}