// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using NLog;
using System;
using System.Security.Cryptography;
using System.Text;

namespace IdentityModel.OidcClient
{
    internal class CryptoHelper
    {
        private static readonly Lazy<Logger> s_ObjLogger = new Lazy<Logger>(LogManager.GetCurrentClassLogger);
        private static Logger Log => s_ObjLogger.Value;
        private readonly OidcClientOptions _options;

        public CryptoHelper(OidcClientOptions options)
        {
            _options = options;
            //Log = options.LoggerFactory.CreateLogger<CryptoHelper>();
        }

        public static HashAlgorithm GetMatchingHashAlgorithm(string signatureAlgorithm)
        {
            Log.Trace("GetMatchingHashAlgorithm");
            Log.Debug("Determining matching hash algorithm for {signatureAlgorithm}", signatureAlgorithm);

            int signingAlgorithmBits = int.Parse(signatureAlgorithm.Substring(signatureAlgorithm.Length - 3));
            
            switch (signingAlgorithmBits)
            {
                case 256:
                    Log.Debug("SHA256");
                    return SHA256.Create();
                case 384:
                    Log.Debug("SHA384");
                    return SHA384.Create();
                case 512:
                    Log.Debug("SHA512");
                    return SHA512.Create();
                default:
                    return null;
            }
        }

        public static bool ValidateHash(string data, string hashedData, string signatureAlgorithm)
        {
            Log.Trace("ValidateHash");

            HashAlgorithm hashAlgorithm = GetMatchingHashAlgorithm(signatureAlgorithm);
            if (hashAlgorithm == null)
            {
                Log.Error("No appropriate hashing algorithm found.");
                return false;
            }

            using (hashAlgorithm)
            {
                byte[] hash = hashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(data));
                int size = (hashAlgorithm.HashSize / 8) / 2;

                byte[] leftPart = new byte[hashAlgorithm.HashSize / size];
                Array.Copy(hash, leftPart, hashAlgorithm.HashSize / size);

                string leftPartB64 = Base64Url.Encode(leftPart);
                bool match = leftPartB64.Equals(hashedData);

                if (!match)
                {
                    Log.Error($"data ({leftPartB64}) does not match hash from token ({hashedData})");
                }

                return match;
            }
        }

        public static string CreateState(int length)
        {
            Log.Trace("CreateState");

            return CryptoRandom.CreateUniqueId(length);
        }

        public static Pkce CreatePkceData()
        {
            Log.Trace("CreatePkceData");

            Pkce pkce = new Pkce
            {
                CodeVerifier = CryptoRandom.CreateUniqueId()
            };

            byte[] challengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes(pkce.CodeVerifier));
            pkce.CodeChallenge = Base64Url.Encode(challengeBytes);

            return pkce;
        }

        internal class Pkce
        {
            public string CodeVerifier { get; set; }
            public string CodeChallenge { get; set; }
        }
    }
}
