﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace twitter
{

    public delegate void TwitterCallback(bool success, string response);

    public class Client
    {

        #region Token
        public static string consumerKey { get; set; }
        public static string consumerSecret { get; set; }
        public static string accessToken { get; set; }
        public static string accessTokenSecret { get; set; }

        #endregion

        #region API Methods
        public static IEnumerator Get(string APIPath, Dictionary<string, string> APIParams, TwitterCallback callback)
        {
            string REQUEST_URL = "https://api.twitter.com/1.1/" + APIPath + ".json";

            SortedDictionary<string, string> parameters = new SortedDictionary<string, string>();
            SetAPIParams(parameters, APIParams);

            string requestURL = REQUEST_URL + "?" + GenerateRequestparams(parameters);
            UnityWebRequest request = UnityWebRequest.Get(requestURL);

            yield return SendRequest(request, parameters, "GET", REQUEST_URL, consumerKey, consumerSecret, accessToken, accessTokenSecret, callback);
        }

        public static IEnumerator Post(string APIPath, Dictionary<string, string> APIParams, TwitterCallback callback)
        {
            string REQUEST_URL = "https://api.twitter.com/1.1/" + APIPath + ".json";

            SortedDictionary<string, string> parameters = new SortedDictionary<string, string>();
            SetAPIParams(parameters, APIParams);

            WWWForm form = new WWWForm();
            foreach (KeyValuePair<string, string> parameter in APIParams)
            {
                form.AddField(parameter.Key, parameter.Value);
            }

            UnityWebRequest request = UnityWebRequest.Post(REQUEST_URL, form);
            yield return SendRequest(request, parameters, "POST", REQUEST_URL, consumerKey, consumerSecret, accessToken, accessTokenSecret, callback);

        }

        public static IEnumerator Stream(string APIPath, Dictionary<string, string> APIParams, TwitterCallback callback)
        {
            string REQUEST_URL = "https://stream.twitter.com/1.1/" + APIPath + ".json";

            SortedDictionary<string, string> parameters = new SortedDictionary<string, string>();
            SetAPIParams(parameters, APIParams);

            WWWForm form = new WWWForm();
            foreach (KeyValuePair<string, string> APIParam in APIParams)
            {
                form.AddField(APIParam.Key, APIParam.Value);
            }

            UnityWebRequest request = UnityWebRequest.Post(REQUEST_URL, form);
            yield return SendRequest(request, parameters, "POST", REQUEST_URL, consumerKey, consumerSecret, accessToken, accessTokenSecret, callback);


        }

        #endregion

        #region RequestHelperMethods

        private static void SetAPIParams(SortedDictionary<string, string> parameters, Dictionary<string, string> APIParams)
        {
            foreach (KeyValuePair<string, string> APIParam in APIParams)
            {
                parameters.Add(APIParam.Key, APIParam.Value);
            }
        }

        private static string GenerateRequestparams(SortedDictionary<string, string> parameters)
        {
            StringBuilder requestParams = new StringBuilder();
            foreach (KeyValuePair<string, string> param in parameters)
            {
                requestParams.Append(UrlEncode(param.Key) + "=" + UrlEncode(param.Value) + "&");
            }
            requestParams.Length -= 1; // Remove "&" at the last of string
            return requestParams.ToString();
        }

        private static IEnumerator SendRequest(UnityWebRequest request, SortedDictionary<string, string> parameters, string method, string requestURL, string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret, TwitterCallback callback)
        {
            request.SetRequestHeader("ContentType", "application/x-www-form-urlencoded");
            request.SetRequestHeader("Authorization", GenerateHeaderWithAccessToken(parameters, method, requestURL, consumerKey, consumerSecret, accessToken, accessTokenSecret));
            yield return request.Send();

            if (request.isError)
            {
                callback(false, JsonHelper.ArrayToObject(request.error));
            }
            else
            {
                if (request.responseCode == 200)
                {
                    callback(true, JsonHelper.ArrayToObject(request.downloadHandler.text));
                }
                else
                {
                    Debug.Log(request.responseCode);
                    callback(false, JsonHelper.ArrayToObject(request.downloadHandler.text));
                }
            }
        }

        #endregion

        #region OAuthHelperMethods

        private static string GenerateHeaderWithAccessToken(SortedDictionary<string, string> parameters, string requestMethod, string requestURL, string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret)
        {
            string signature = GenerateSignatureWithAccessToken(parameters, requestMethod, requestURL, consumerKey, consumerSecret, accessToken, accessTokenSecret);

            StringBuilder requestParamsString = new StringBuilder();
            foreach (KeyValuePair<string, string> param in parameters)
            {
                requestParamsString.Append(String.Format("{0}=\"{1}\",", UrlEncode(param.Key), UrlEncode(param.Value)));
            }

            string authHeader = "OAuth realm=\"Twitter API\",";
            string requestSignature = String.Format("oauth_signature=\"{0}\"", UrlEncode(signature));
            authHeader += requestParamsString.ToString() + requestSignature;
            return authHeader;
        }

        private static string GenerateSignatureWithAccessToken(SortedDictionary<string, string> parameters, string requestMethod, string requestURL, string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret)
        {
            AddDefaultOauthParams(parameters, consumerKey);
            parameters.Add("oauth_token", accessToken);

            StringBuilder paramString = new StringBuilder();
            foreach (KeyValuePair<string, string> param in parameters)
            {
                paramString.Append(UrlEncode(param.Key) + "=" + UrlEncode(param.Value) + "&");
            }
            paramString.Length -= 1; // Remove "&" at the last of string


            string requestHeader = UrlEncode(requestMethod) + "&" + UrlEncode(requestURL);
            string signatureData = requestHeader + "&" + UrlEncode(paramString.ToString());

            string signatureKey = UrlEncode(consumerSecret) + "&" + UrlEncode(accessTokenSecret);
            HMACSHA1 hmacsha1 = new HMACSHA1(Encoding.ASCII.GetBytes(signatureKey));
            byte[] signatureBytes = hmacsha1.ComputeHash(Encoding.ASCII.GetBytes(signatureData));
            return Convert.ToBase64String(signatureBytes);
        }

        private static void AddDefaultOauthParams(SortedDictionary<string, string> parameters, string consumerKey)
        {
            parameters.Add("oauth_consumer_key", consumerKey);
            parameters.Add("oauth_signature_method", "HMAC-SHA1");
            parameters.Add("oauth_timestamp", GenerateTimeStamp());
            parameters.Add("oauth_nonce", GenerateNonce());
            parameters.Add("oauth_version", "1.0");
        }

        private static string GenerateTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }

        private static string GenerateNonce()
        {
            return new System.Random().Next(123400, int.MaxValue).ToString("X");
        }

        private static string UrlEncode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = Uri.EscapeDataString(value);
            value = Regex.Replace(value, "(%[0-9a-f][0-9a-f])", c => c.Value.ToUpper());
            value = value
                .Replace("(", "%28")
                .Replace(")", "%29")
                .Replace("$", "%24")
                .Replace("!", "%21")
                .Replace("*", "%2A")
                .Replace("'", "%27");
            value = value.Replace("%7E", "~");
            return value;

        }
    }

    #endregion

}