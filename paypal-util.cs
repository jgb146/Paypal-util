using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace PayPalUtil
{ /* Rubicite Interactive's Paypal Utility package, v. 1.0
   * Last Modified 7 June 2012
   * Copywrite 2012, Rubicite Interactive Inc.
   * 
   * Use and sharing: You may use, modify, and distribute this code freely for all purposes (both private and commercial) so long as the copywrite notice and sharing details are preserved in this comment.
   *                  All use of this code is at your own risk. Rubicite Interactive makes no assurances that it is bug free or up to date. 
   *                  Rubicite Interactive accepts no responsibility for any problems that occur resulting from use of this code package.
   * 
   * This file is a collection of functions to help with processing Paypal data; 
   * For tips on using this (if you have access), see https://docs.google.com/document/d/1ZoMUmbRGk4lEJ_3Yy6oHvv5lfyklLTCcngQlqqm7Juw/edit
   * 
   */

    public class pdt_handler
    { //This class contains helper functions for processing Payment Data Transfer requests from PayPal

        public string paypalStr_txID;
        public string paypalStr_IdentityToken;
        public Dictionary<string,string> dic_data;

        public pdt_handler(string pStr_txID, string pStr_IdentityToken, Boolean pBool_useSandbox = false)
        {
            paypalStr_txID = pStr_txID;
            paypalStr_IdentityToken = pStr_IdentityToken;
            dic_data = processRequest(paypalStr_txID, paypalStr_IdentityToken, pBool_useSandbox);
        }

        public Dictionary<string, string> processRequest(string pStr_txID = "", string pStr_IdentityToken = "", Boolean pBool_useSandbox = false)
        {// process the PDT request this object, or for the transaction specified

            //allow specifying via params to support running this without instantiating an object
            if (pStr_txID == "") pStr_txID = paypalStr_txID;
            if (pStr_IdentityToken == "") pStr_IdentityToken = paypalStr_IdentityToken;


            //-----Issue a request to https://www.paypal.com/cgi-bin/webscr to receive the PDT data from PayPal -----
            HttpWebRequest req = null;
            if (pBool_useSandbox) req = (HttpWebRequest)WebRequest.Create("https://www.sandbox.paypal.com/cgi-bin/webscr");
            else req = (HttpWebRequest)WebRequest.Create("https://www.paypal.com/cgi-bin/webscr");

            //Set values for the request 
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            string strRequest = "";
            strRequest += "cmd=_notify-synch";
            strRequest += "&tx=" + pStr_txID;
            strRequest += "&at=" + paypalStr_IdentityToken;
            req.ContentLength = strRequest.Length;
            
            //Send the request to PayPal and get the response
            StreamWriter streamOut = new StreamWriter(req.GetRequestStream(), System.Text.Encoding.ASCII);
            streamOut.Write(strRequest);
            streamOut.Close();
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            
            //-----Process the PDT data in the response -----
            
            //reject invalid responses 
            if (response.StatusCode != HttpStatusCode.OK)
                return null;

            //load response into string
            StreamReader streamIn = new StreamReader(response.GetResponseStream());
            string strResponse = streamIn.ReadToEnd();
            streamIn.Close();

            //reject if transaction was not successful
            if (strResponse.Substring(0, 7).ToLower() != "success")
                return null;

            //split into lines
            char[] aChr_newLine = {'\r', '\n'};
            string[] aStr_lines = strResponse.Split(aChr_newLine);

            //pull data out of each line and insert it into results
            Dictionary<string, string> aStr_results = new Dictionary<string, string>();
            foreach (string fStr_line in aStr_lines)
            {
                if (fStr_line.Contains('='))
                {
                    string[] parts = fStr_line.Trim().Split('=');
                    if ((parts.Length > 1) && (parts[0] != ""))
                        aStr_results[parts[0].Trim()] = parts[1].Trim();
                }
                    
            }

            return aStr_results;
        }
    }

    public class ipn_handler
    {//This class contains helper functions for processing IPN data from PayPal

        public Dictionary<string,string> dic_data;

        public ipn_handler(Boolean pBool_useSandbox = false)
        {
            dic_data = processIpnPost(pBool_useSandbox);
        }
        
        public Dictionary<string, string> processIpnPost(bool pBool_useSandbox = false)
        {//send IPN response and process paypal IPN post; This should be the first action, since paypal resends IPNs after a time-delay
            //  based on code from https://www.paypal.com/us/cgi-bin/webscr?cmd=p/pdn/ipn-codesamples-pop-outside 

            //Post back to either sandbox or live
            string strSandbox = "https://www.sandbox.paypal.com/cgi-bin/webscr";
            string strLive = "https://www.paypal.com/cgi-bin/webscr";
            HttpWebRequest req;
            if (pBool_useSandbox)
            { req = (HttpWebRequest)WebRequest.Create(strSandbox); }
            else
            { req = (HttpWebRequest)WebRequest.Create(strLive); }

            //Set values for the request back
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            byte[] param = HttpContext.Current.Request.BinaryRead(HttpContext.Current.Request.ContentLength);
            string strRequest = Encoding.ASCII.GetString(param);
            strRequest += "&cmd=_notify-validate";
            req.ContentLength = strRequest.Length;

            //for proxy
            //WebProxy proxy = new WebProxy(new Uri("http://url:port#"));
            //req.Proxy = proxy;

            //Send the request to PayPal and get the response
            StreamWriter streamOut = new StreamWriter(req.GetRequestStream(), System.Text.Encoding.ASCII);
            streamOut.Write(strRequest);
            streamOut.Close();
            StreamReader streamIn = new StreamReader(req.GetResponse().GetResponseStream());
            string strResponse = streamIn.ReadToEnd();
            streamIn.Close();

            Dictionary<string, string> dic_data = new Dictionary<string, string>();

            if (strResponse == "VERIFIED")
            {// Don't forget to check the results we get back. Where applicable,
                //check the payment_status is Completed
                //check that txn_id has not been previously processed
                //check that receiver_email is your Primary PayPal email
                //check that payment_amount/payment_currency are correct

                dic_data.Add("response", "VERIFIED");
                
                //load keys and values into dictionary
                System.Collections.Specialized.NameValueCollection postedValues = HttpContext.Current.Request.Form;
                String nextKey;

                for (int i = 0; i < postedValues.AllKeys.Length; i++)
                {
                    nextKey = postedValues.AllKeys[i];
                    if (nextKey.Substring(0, 2) != "__")
                    { dic_data.Add(nextKey, postedValues[i]); }
                }

            }
            else if (strResponse == "INVALID")
            {
                //log for manual investigation
                dic_data.Add("response", "INVALID");
            }
            else
            {
                //log response/ipn data for manual investigation
                dic_data.Add("response", "UNRECOGNIZED");
            }

            return dic_data;
        }
    }
}