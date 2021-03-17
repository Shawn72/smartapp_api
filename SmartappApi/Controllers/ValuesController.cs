using System;
using System.Collections.Generic;
using System.Web.Http.Cors;
using System.Web.Http;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using SmartappApi.Models;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace SmartappApi.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*", exposedHeaders: "X-My-Header")]
    [BasicAuthentication]
    public class ValuesController : ApiController
    {
        ///for use on localhost testings
        public static string Baseurl = ConfigurationManager.AppSettings["API_LOCALHOST_URL"];

        /// API Authentications
        public static string ApiUsername = ConfigurationManager.AppSettings["API_USERNAME"];
        public static string ApiPassword = ConfigurationManager.AppSettings["API_PWD"];

        /// API Authentications

        /// MySQL Connection string
        public static readonly string ConString = new DBConfig().MysqLConnector();

        [Route("api/Values")]

        [HttpPost]
        [Route("api/AddNewUser")]
        public IHttpActionResult AddNewUser([FromBody] SignUpModel signUpModel)
        {
            MySqlTransaction mysqlTrx = (dynamic)null;
            MySqlConnection con = (dynamic)null;
         
            try
            {
                if (!ModelState.IsValid)
                { return BadRequest(ModelState); }

                //now add supplied data to dB

                if (string.IsNullOrWhiteSpace(signUpModel.Fname))
                    return Json("FnameEmpty");
                if (string.IsNullOrWhiteSpace(signUpModel.Mname))
                    return Json("MnameEmpty");
                if (string.IsNullOrWhiteSpace(signUpModel.Lname))
                    return Json("LnameEmpty");
                if (string.IsNullOrWhiteSpace(signUpModel.IdNumber))
                    return Json("IdNumberEmpty");
                if (string.IsNullOrWhiteSpace(signUpModel.PhoneNumber))
                    return Json("PhonenumberEmpty");
                if (string.IsNullOrWhiteSpace(signUpModel.AccountNumber))
                    return Json("AccountnumberEmpty");
                if (string.IsNullOrWhiteSpace(signUpModel.Email))
                    return Json("EmailEmpty");
                if (string.IsNullOrWhiteSpace(signUpModel.Password1))
                    return Json("Password1Empty");
                if (string.IsNullOrWhiteSpace(signUpModel.Password2))
                    return Json("Password2Empty");
                if (signUpModel.Password1!=signUpModel.Password2)
                    return Json("PasswordsMismatched");

              
                using (con = new MySqlConnection(ConString))
                {
                    string insertQry =
                        "INSERT INTO users(fname, mname, lname, email, id_number, mobile_number, password) VALUES(@fname, @mname, @lname, @email, @idnumber, @mobilenumber, @password)";
                    
                    con.Open();
                    MySqlCommand command = new MySqlCommand(insertQry, con);
                    // Start a local transaction
                    mysqlTrx = con.BeginTransaction();
                    // assign both transaction object and connection to Command object for a pending local transaction
                    command.Connection = con;
                    command.Transaction = mysqlTrx;

                    //use parametarized queries to prevent sql injection
                    command.Parameters.AddWithValue("@fname", signUpModel.Fname);
                    command.Parameters.AddWithValue("@mname", signUpModel.Mname);
                    command.Parameters.AddWithValue("@lname", signUpModel.Lname);
                    command.Parameters.AddWithValue("@email", signUpModel.Email);
                    command.Parameters.AddWithValue("@idnumber", signUpModel.IdNumber);
                    command.Parameters.AddWithValue("@mobilenumber", signUpModel.PhoneNumber);
                    command.Parameters.AddWithValue("@password", EncryptP(signUpModel.Password2));

                    if (command.ExecuteNonQuery() == 1)
                    {
                        var userid = (dynamic)null;

                        //insert to bank details table
                        //get userid first
                        string checkifUserExists = "SELECT * FROM users WHERE email = @email LIMIT 1";
                        //check if agent exists first
                        MySqlCommand commandX = new MySqlCommand(checkifUserExists, con);
                        //use parametarized queries to prevent sql injection
                        commandX.Parameters.AddWithValue("@email", signUpModel.Email);
                        commandX.ExecuteNonQuery();
                        DataTable dt = new DataTable();
                        MySqlDataAdapter da = new MySqlDataAdapter(commandX);
                        da.Fill(dt);
                        foreach (DataRow dr in dt.Rows)
                        {  userid = dr["user_id"]; }

                        string insertQrytoBankdetails =
                            "INSERT INTO user_bank_details(user_id, account_number, account_name, account_balance, account_status) " +
                            "VALUES(@userid, @accountnumber, @accountname, @accountbalance, @accountstatus)";

                        MySqlCommand commandP = new MySqlCommand(insertQrytoBankdetails, con);
                        //use parametarized queries to prevent sql injection
                        commandP.Parameters.AddWithValue("@userid", userid);
                        commandP.Parameters.AddWithValue("@idnumber", signUpModel.IdNumber);
                        commandP.Parameters.AddWithValue("@mobilenumber", signUpModel.PhoneNumber);
                        commandP.Parameters.AddWithValue("@accountnumber", signUpModel.AccountNumber);
                        commandP.Parameters.AddWithValue("@accountname", signUpModel.Fname+" "+signUpModel.Lname);
                        commandP.Parameters.AddWithValue("@accountbalance", Convert.ToDecimal(0));
                        commandP.Parameters.AddWithValue("@accountstatus", "active");

                        if (commandP.ExecuteNonQuery() == 1)
                        {
                            //commit the insert transaction to dB  after inserting to 2nd table
                            mysqlTrx.Commit();
                            return Ok("success");
                        }
                        
                    }
                    con.Close();
                    return Ok("Error Occured!");
                }
            }
            catch (MySqlException ex)
            {
                try
                {
                    //rollback the transaction if any error occurs during the process of inserting
                    con.Open();
                    mysqlTrx.Rollback();
                    con.Close();
                }
                catch (MySqlException ex2)
                {
                    if (mysqlTrx.Connection != null)
                    {
                        return Ok("An exception of type " + ex2.GetType() + " was encountered while attempting to roll back the transaction!");
                    }
                }
                return Ok("No record was inserted due to this error: " + ex.Message);
            }
            finally
            {
                con.Close();
            }

        }
        
        [HttpPost]
        [Route("api/AddDeposit")]
        public IHttpActionResult AddDeposit([FromBody] DepositsAndWithdrawalsModel depowithdrawModel)
        {
            MySqlTransaction mysqlTrx = (dynamic)null;
            MySqlConnection con = (dynamic)null;

            try
            {
                if (!ModelState.IsValid)
                { return BadRequest(ModelState); }

                //now add supplied data to dB
                
                if (string.IsNullOrWhiteSpace(depowithdrawModel.AccountNumber))
                    return Json("AccountNumberEmpty");

                if (string.IsNullOrWhiteSpace(depowithdrawModel.AmountToDeposit.ToString(CultureInfo.InvariantCulture)))
                    return Json("AmountEmpty");
              
                using (con = new MySqlConnection(ConString))
                {

                    string checkifAccountExists = "SELECT * FROM user_bank_details WHERE account_number = @accountnumber LIMIT 1";
                    //check if user exists first
                    con.Open();
                    MySqlCommand commandXt = new MySqlCommand(checkifAccountExists, con);

                    //use parametarized queries to prevent sql injection
                    commandXt.Parameters.AddWithValue("@accountnumber", depowithdrawModel.AccountNumber);

                    int accountIsThere = (int)commandXt.ExecuteScalar();

                    if (accountIsThere == 1)
                    {
                        //get account_id, user_id
                        var accountid = (dynamic) null;
                        var userid = (dynamic)null;

                        DataTable dt = new DataTable();
                        MySqlDataAdapter da = new MySqlDataAdapter(commandXt);
                        da.Fill(dt);
                        foreach (DataRow dr in dt.Rows)
                        {
                            accountid = dr["id"];
                            userid = dr["user_id"];
                        }

                        //account exist, now update it with deposit
                        string updateQry =
                            "UPDATE user_bank_details SET account_balance = account_balance+@amounttodeposit WHERE account_number=@account_number";

                        MySqlCommand commandUpdt = new MySqlCommand(updateQry, con);
                        // Start a local transaction
                        mysqlTrx = con.BeginTransaction();
                        // assign both transaction object and connection to Command object for a pending local transaction
                        commandUpdt.Connection = con;
                        commandUpdt.Transaction = mysqlTrx;

                        //use parametarized queries to prevent sql injection
                        commandUpdt.Parameters.AddWithValue("@amounttodeposit", depowithdrawModel.AmountToDeposit);
                        commandUpdt.Parameters.AddWithValue("@account_number", depowithdrawModel.AccountNumber);

                        if (commandUpdt.ExecuteNonQuery() == 1)
                        {
                            //update is success, now update transactions table


                            string insertQrytoTrxs =
                                "INSERT INTO general_transactions(user_id, account_id, transaction_description, account_number_paid_to, transaction_amount, transaction_type, transaction_date) " +
                                "VALUES(@userid, @accountid, @trxdescription, @accountnumberpaidto, @trxamount, @trxtype, @trxdate)";

                            MySqlCommand commandP = new MySqlCommand(insertQrytoTrxs, con);
                            //use parametarized queries to prevent sql injection
                            commandP.Parameters.AddWithValue("@userid", userid);
                            commandP.Parameters.AddWithValue("@accountid", accountid);
                            commandP.Parameters.AddWithValue("@trxdescription", "Deposit to Account");
                            commandP.Parameters.AddWithValue("@accountnumberpaidto", depowithdrawModel.AccountNumber);
                            commandP.Parameters.AddWithValue("@trxamount", depowithdrawModel.AmountToDeposit);
                            commandP.Parameters.AddWithValue("@trxtype", "Deposit");
                            commandP.Parameters.AddWithValue("@trxdate", DateTime.Now);

                            if (commandP.ExecuteNonQuery() == 1)
                            {
                                //now commit
                                mysqlTrx.Commit();
                                return Ok("deposit success");

                            }
                            
                        }
                    }
                    con.Close();
                    return Json("Account absent");
                }
            }
            catch (MySqlException ex)
            {
                try
                {
                    //rollback the transaction if any error occurs during the process of inserting
                    con.Open();
                    mysqlTrx.Rollback();
                    con.Close();
                }
                catch (MySqlException ex2)
                {
                    if (mysqlTrx.Connection != null)
                    {
                        return Ok("An exception of type " + ex2.GetType() + " was encountered while attempting to roll back the transaction!");
                    }
                }
                return Ok("No record was inserted due to this error: " + ex.Message);
            }
            finally
            {
                con.Close();
            }

        }
       
        [HttpPost]
        [Route("api/AddBill")]
        public IHttpActionResult AddBill([FromBody] BillsModel billModel)
        {
            MySqlTransaction mysqlTrx = (dynamic)null;
            MySqlConnection con = (dynamic)null;

            try
            {
                if (!ModelState.IsValid)
                { return BadRequest(ModelState); }

                //now add supplied data to dB

                if (string.IsNullOrWhiteSpace(billModel.BillDescription))
                    return Json("BillDescriptionEmpty");


                if (string.IsNullOrWhiteSpace(billModel.BillaccountNumber))
                    return Json("BillAccountnumberEmpty");


                    using (con = new MySqlConnection(ConString))
                    {


                        string insertQrytoBills =
                            "INSERT INTO bills(description, bill_account_no" +
                            "VALUES(@billdescription, @billaccno)";

                        con.Open();
                        MySqlCommand commandP = new MySqlCommand(insertQrytoBills, con);
                        //use parametarized queries to prevent sql injection
                        commandP.Parameters.AddWithValue("@billdescription", billModel.BillDescription);
                        commandP.Parameters.AddWithValue("@billaccno", billModel.BillaccountNumber);

                        if (commandP.ExecuteNonQuery() == 1)
                        {
                            //now commit
                            mysqlTrx.Commit();
                            return Ok("bill saved success");
                        }

                    }
                    
                con.Close();
                return Json("Error Occured!");
                
            }
            catch (MySqlException ex)
            {
                try
                {
                    //rollback the transaction if any error occurs during the process of inserting
                    con.Open();
                    mysqlTrx.Rollback();
                    con.Close();
                }
                catch (MySqlException ex2)
                {
                    if (mysqlTrx.Connection != null)
                    {
                        return Ok("An exception of type " + ex2.GetType() + " was encountered while attempting to roll back the transaction!");
                    }
                }
                return Ok("No record was inserted due to this error: " + ex.Message);
            }
            finally
            {
                con.Close();
            }

        }
       
        [HttpPost]
        [Route("api/AddInvestment")]
        public IHttpActionResult AddInvestment([FromBody] InvestmentModel investmentModel)
        {
            MySqlTransaction mysqlTrx = (dynamic)null;
            MySqlConnection con = (dynamic)null;

            try
            {
                if (!ModelState.IsValid)
                { return BadRequest(ModelState); }

                //now add supplied data to dB

                if (string.IsNullOrWhiteSpace(investmentModel.InvestmentCode))
                    return Json("InvestmentCodeEmpty");


                if (string.IsNullOrWhiteSpace(investmentModel.InvestmentDescription))
                    return Json("InvestmentDescriptionEmpty");

                if (string.IsNullOrWhiteSpace(investmentModel.InvestmentValue.ToString(CultureInfo.InvariantCulture)))
                    return Json("InvestmentValueEmpty");

                using (con = new MySqlConnection(ConString))
                {


                    string insertQrytoBills =
                        "INSERT INTO investments(investment_code, investment_description, investment_value)" +
                        "VALUES(@inveCode, @inveDescr, @inveVal)";

                    con.Open();
                    MySqlCommand commandP = new MySqlCommand(insertQrytoBills, con);
                    // Start a local transaction
                    mysqlTrx = con.BeginTransaction();
                    // assign both transaction object and connection to Command object for a pending local transaction
                    commandP.Connection = con;
                    commandP.Transaction = mysqlTrx;

                    //use parametarized queries to prevent sql injection
                    commandP.Parameters.AddWithValue("@inveCode", investmentModel.InvestmentCode);
                    commandP.Parameters.AddWithValue("@inveDescr", investmentModel.InvestmentDescription);
                    commandP.Parameters.AddWithValue("@inveVal", investmentModel.InvestmentValue);

                    if (commandP.ExecuteNonQuery() == 1)
                    {
                        //now commit
                        mysqlTrx.Commit();
                        return Ok("investment saved success");
                    }

                }

                con.Close();
                return Json("Error Occured!");

            }
            catch (MySqlException ex)
            {
                try
                {
                    //rollback the transaction if any error occurs during the process of inserting
                    con.Open();
                    mysqlTrx.Rollback();
                    con.Close();
                }
                catch (MySqlException ex2)
                {
                    if (mysqlTrx.Connection != null)
                    {
                        return Ok("An exception of type " + ex2.GetType() + " was encountered while attempting to roll back the transaction!");
                    }
                }
                return Ok("No record was inserted due to this error: " + ex.Message);
            }
            finally
            {
                con.Close();
            }

        }



        [HttpPost]
        [Route("api/ActivateAccount")]
        public IHttpActionResult ActivateAccount([FromBody] SignUpModel signUpModel)
        {
            MySqlTransaction mysqlTrx = (dynamic)null;
            MySqlConnection con = (dynamic)null;

            try
            {
                if (!ModelState.IsValid)
                { return BadRequest(ModelState); }

                //now add supplied data to dB

                if (string.IsNullOrWhiteSpace(signUpModel.AccountNumber))
                    return Json("AccountnumberEmpty");

                using (con = new MySqlConnection(ConString))
                {


                    string insertQrytoBills =
                        "UPDATE user_bank_details SET account_status =@Status WHERE account_number = @accountNumber";

                    con.Open();
                    MySqlCommand commandUpdt = new MySqlCommand(insertQrytoBills, con);
                 
                    // Start a local transaction
                    mysqlTrx = con.BeginTransaction();
                    // assign both transaction object and connection to Command object for a pending local transaction
                    commandUpdt.Connection = con;
                    commandUpdt.Transaction = mysqlTrx;
                    //use parametarized queries to prevent sql injection
                    commandUpdt.Parameters.AddWithValue("@Status",  "active");
                    commandUpdt.Parameters.AddWithValue("@accountNumber", signUpModel.AccountNumber);

                    if (commandUpdt.ExecuteNonQuery() == 1)
                    {
                        //now commit
                        mysqlTrx.Commit();
                        return Ok("Account activated success");
                    }

                }

                con.Close();
                return Json("Error Occured!");

            }
            catch (MySqlException ex)
            {
                try
                {
                    //rollback the transaction if any error occurs during the process of inserting
                    con.Open();
                    mysqlTrx.Rollback();
                    con.Close();
                }
                catch (MySqlException ex2)
                {
                    if (mysqlTrx.Connection != null)
                    {
                        return Ok("An exception of type " + ex2.GetType() + " was encountered while attempting to roll back the transaction!");
                    }
                }
                return Ok("No record was inserted due to this error: " + ex.Message);
            }
            finally
            {
                con.Close();
            }
        }


        [HttpPost]
        [Route("api/DeactivateAccount")]
        public IHttpActionResult DeactivateAccount([FromBody] SignUpModel signUpModel)
        {
            MySqlTransaction mysqlTrx = (dynamic)null;
            MySqlConnection con = (dynamic)null;

            try
            {
                if (!ModelState.IsValid)
                { return BadRequest(ModelState); }

                //now add supplied data to dB

                if (string.IsNullOrWhiteSpace(signUpModel.AccountNumber))
                    return Json("AccountnumberEmpty");

                using (con = new MySqlConnection(ConString))
                {


                    string insertQrytoBills =
                        "UPDATE user_bank_details SET account_status =@Status WHERE account_number = @accountNumber";

                    con.Open();
                    MySqlCommand commandUpdt = new MySqlCommand(insertQrytoBills, con);

                    // Start a local transaction
                    mysqlTrx = con.BeginTransaction();
                    // assign both transaction object and connection to Command object for a pending local transaction
                    commandUpdt.Connection = con;
                    commandUpdt.Transaction = mysqlTrx;
                    //use parametarized queries to prevent sql injection
                    commandUpdt.Parameters.AddWithValue("@Status", "inactive");
                    commandUpdt.Parameters.AddWithValue("@accountNumber", signUpModel.AccountNumber);

                    if (commandUpdt.ExecuteNonQuery() == 1)
                    {
                        //now commit
                        mysqlTrx.Commit();
                        return Ok("Account Deactivated success");
                    }

                }

                con.Close();
                return Json("Error Occured!");

            }
            catch (MySqlException ex)
            {
                try
                {
                    //rollback the transaction if any error occurs during the process of inserting
                    con.Open();
                    mysqlTrx.Rollback();
                    con.Close();
                }
                catch (MySqlException ex2)
                {
                    if (mysqlTrx.Connection != null)
                    {
                        return Ok("An exception of type " + ex2.GetType() + " was encountered while attempting to roll back the transaction!");
                    }
                }
                return Ok("No record was inserted due to this error: " + ex.Message);
            }
            finally
            {
                con.Close();
            }
        }



        [HttpGet]
        [Route("api/GetUsers")]
        public IHttpActionResult GetUsers()
        {
            using (MySqlConnection con = new MySqlConnection(ConString))
            {
                con.Open();
                string selectQuery = "SELECT * FROM users";
                MySqlCommand command0 = new MySqlCommand(selectQuery, con);
                command0.ExecuteNonQuery();
                DataTable dt = new DataTable();
                MySqlDataAdapter da = new MySqlDataAdapter(command0);
                da.Fill(dt);
                con.Close();
                return Json(dt);
            }
        }

        [HttpGet]
        [Route("api/GetBankDetails")]
        public IHttpActionResult GetBankDetails()
        {
            using (MySqlConnection con = new MySqlConnection(ConString))
            {
                con.Open();
                string selectQuery = "SELECT * FROM user_bank_details";
                MySqlCommand command0 = new MySqlCommand(selectQuery, con);
                command0.ExecuteNonQuery();
                DataTable dt = new DataTable();
                MySqlDataAdapter da = new MySqlDataAdapter(command0);
                da.Fill(dt);
                con.Close();
                return Json(dt);
            }
        }

        [HttpGet]
        [Route("api/GetGeneralTransactions")]
        public IHttpActionResult GetGeneralTransactions()
        {
            using (MySqlConnection con = new MySqlConnection(ConString))
            {
                con.Open();
                string selectQuery = "SELECT * FROM general_transactions";
                MySqlCommand command0 = new MySqlCommand(selectQuery, con);
                command0.ExecuteNonQuery();
                DataTable dt = new DataTable();
                MySqlDataAdapter da = new MySqlDataAdapter(command0);
                da.Fill(dt);
                con.Close();
                return Json(dt);
            }
        }

        [HttpGet]
        [Route("api/GetAllInvestmentsList")]
        public IHttpActionResult GetAllInvestmentsList()
        {
            using (MySqlConnection con = new MySqlConnection(ConString))
            {
                con.Open();
                string selectQuery = "SELECT * FROM investments";
                MySqlCommand command0 = new MySqlCommand(selectQuery, con);
                command0.ExecuteNonQuery();
                DataTable dt = new DataTable();
                MySqlDataAdapter da = new MySqlDataAdapter(command0);
                da.Fill(dt);
                con.Close();
                return Json(dt);
            }
        }

        static string EncryptP(string mypass)
        {
            //encryptpassword:
            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                UTF8Encoding utf8 = new UTF8Encoding();
                byte[] data = md5.ComputeHash(utf8.GetBytes(mypass));
                return Convert.ToBase64String(data);
            }
        }
    }
}
