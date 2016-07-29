﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
//using System.Security.Cryptography;
using System.Text;
using PCLCrypto;
using System.Net.Http;
using Newtonsoft.Json;
using Flurl;
using Flurl.Http;

namespace GPSOAuthSharp
{
    // gpsoauth:__init__.py
    // URL: https://github.com/simon-weber/gpsoauth/blob/master/gpsoauth/__init__.py
    public class GPSOAuthClient
    {
        static string b64Key = "AAAAgMom/1a/v0lblO2Ubrt60J2gcuXSljGFQXgcyZWveWLEwo6prwgi3" +
            "iJIZdodyhKZQrNWp5nKJ3srRXcUW+F1BD3baEVGcmEgqaLZUNBjm057pK" +
            "RI16kB0YppeGx5qIQ5QjKzsR8ETQbKLNWgRY0QRNVz34kMJR3P/LgHax/" +
            "6rmf5AAAAAwEAAQ==";
        static RSAParameters androidKey = GoogleKeyUtils.KeyFromB64(b64Key);

        static string version = "0.0.5";
        static string authUrl = "https://android.clients.google.com/auth";
        static string userAgent = "GPSOAuthSharp/" + version;

        private string email;
        private string password;

        public GPSOAuthClient(string email, string password)
        {
            this.email = email;
            this.password = password;
        }

        // _perform_auth_request
        private Dictionary<string, string> PerformAuthRequest(object data)
        {
            string result = string.Empty;
            try
            {
                authUrl.WithHeader("User-Agent", userAgent)
                    .PostUrlEncodedAsync(data)
                    .ReceiveString()
                    .ContinueWith
                    (
                        (e) =>
                        {
                            result = e.Result;
                        }
                    )
                    .Wait();
            }
            catch (WebException ex)
            {
                result = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
            }

            return GoogleKeyUtils.ParseAuthResponse(result);
        }

        // perform_master_login
        public Dictionary<string, string> PerformMasterLogin(string service = "ac2dm",
            string deviceCountry = "us", string operatorCountry = "us", string lang = "en", int sdkVersion = 21)
        {
            string signature = GoogleKeyUtils.CreateSignature(email, password, androidKey);
            var data = new
            {
                accountType = "HOSTED_OR_GOOGLE",
                Email = email,
                has_permission = 1.ToString(),
                add_account = 1.ToString(),
                EncryptedPasswd = signature,
                service = service,
                source = "android",
                device_country = deviceCountry,
                operatorCountry = operatorCountry,
                lang = lang,
                sdk_version = sdkVersion.ToString()
            };
            return PerformAuthRequest(data);
        }

        // perform_oauth
        public Dictionary<string, string> PerformOAuth(string masterToken, string service, string app, string clientSig,
            string deviceCountry = "us", string operatorCountry = "us", string lang = "en", int sdkVersion = 21)
        {

            var data = new
            {
                accountType = "HOSTED_OR_GOOGLE",
                Email = email,
                has_permission = 1.ToString(),
                add_account = 1.ToString(),
                EncryptedPasswd = masterToken,
                service = service,
                source = "android",
                app = app,
                client_sig = clientSig,
                device_country = deviceCountry,
                operatorCountry = operatorCountry,
                lang = lang,
                sdk_version = sdkVersion.ToString()
            };
            return PerformAuthRequest(data);
        }
    }

    // gpsoauth:google.py
    // URL: https://github.com/simon-weber/gpsoauth/blob/master/gpsoauth/google.py
    class GoogleKeyUtils
    {
        // key_from_b64
        // BitConverter has different endianness, hence the Reverse()
        public static RSAParameters KeyFromB64(string b64Key)
        {
            byte[] decoded = Convert.FromBase64String(b64Key);
            int modLength = BitConverter.ToInt32(decoded.Take(4).Reverse().ToArray(), 0);
            byte[] mod = decoded.Skip(4).Take(modLength).ToArray();
            int expLength = BitConverter.ToInt32(decoded.Skip(modLength + 4).Take(4).Reverse().ToArray(), 0);
            byte[] exponent = decoded.Skip(modLength + 8).Take(expLength).ToArray();
            RSAParameters rsaKeyInfo = new RSAParameters();
            rsaKeyInfo.Modulus = mod;
            rsaKeyInfo.Exponent = exponent;
            return rsaKeyInfo;
        }

        // key_to_struct
        // Python version returns a string, but we use byte[] to get the same results
        public static byte[] KeyToStruct(RSAParameters key)
        {
            byte[] modLength = { 0x00, 0x00, 0x00, 0x80 };
            byte[] mod = key.Modulus;
            byte[] expLength = { 0x00, 0x00, 0x00, 0x03 };
            byte[] exponent = key.Exponent;
            return DataTypeUtils.CombineBytes(modLength, mod, expLength, exponent);
        }

        // parse_auth_response
        public static Dictionary<string, string> ParseAuthResponse(string text)
        {
            Dictionary<string, string> responseData = new Dictionary<string, string>();
            foreach (string line in text.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('=');
                responseData.Add(parts[0], parts[1]);
            }
            return responseData;
        }

        // signature
        public static string CreateSignature(string email, string password, RSAParameters key)
        {
            IAsymmetricKeyAlgorithmProvider alg = PCLCrypto.WinRTCrypto.AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithm.RsaOaepSha1);
            var importedKey = alg.ImportParameters(key);
            var hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(HashAlgorithm.Sha1);
            byte[] prefix = { 0x00 };
            byte[] keyArray = GoogleKeyUtils.KeyToStruct(key);
            byte[] something = Encoding.UTF8.GetBytes(email + "\x00" + password);
            byte[] hash = hasher.HashData(keyArray).Take(4).ToArray();
            byte[] encrypted = WinRTCrypto.CryptographicEngine.Encrypt(importedKey, something, null);
            byte[] combinedBytes = DataTypeUtils.CombineBytes(prefix, hash, encrypted);
            return DataTypeUtils.UrlSafeBase64(combinedBytes);
        }
    }

    class DataTypeUtils
    {
        public static string UrlSafeBase64(byte[] byteArray)
        {
            return Convert.ToBase64String(byteArray).Replace('+', '-').Replace('/', '_');
        }

        public static byte[] CombineBytes(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
    }
}
