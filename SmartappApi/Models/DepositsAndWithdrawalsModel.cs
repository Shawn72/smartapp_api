namespace SmartappApi.Models
{
    public class DepositsAndWithdrawalsModel
    {
        public string UserId { get; set; }
        public string AccountNumber { get; set; }
        public decimal AmountToDeposit { get; set; }
        public decimal AmountToWithdraw { get; set; }

        //for fetching
        public string user_id { get; set; }
        public string id_number { get; set; }
        public string mobile_number { get; set; }
        public string account_number { get; set; }
        public string account_name { get; set; }
        public decimal account_balance { get; set; }
        public string account_status { get; set; }

        
    }
}