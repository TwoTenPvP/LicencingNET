using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace LicencingNET.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            /* GENERATE KEYS */
            /* PRODUCTION WOULD USE CERTIFICATES INSTEAD */
            RSAParameters privateKey;
            RSAParameters publicKey;
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
            {
                privateKey = rsa.ExportParameters(true);
                publicKey = rsa.ExportParameters(false);
            }

            // Create unsigned licence
            Licence licence = Licence.Create(null, DateTime.Now, DateTime.Now.AddDays(30), new Dictionary<string, string>()
            {
                { "LicenceType", "Trial" },
                { "CustomerName", "John Doe" },
                { "CustomerEmail", "john.doe@contoso.com" },
                { "CustomerCompany", "Contoso Ltd." }
            });

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportParameters(privateKey);

                // Sign the licence with the private key.
                if (licence.Sign(rsa))
                {
                    Console.WriteLine("Signed");
                }
                else
                {
                    Console.WriteLine("Failed to sign");
                }
            }

            byte[] binaryLicence = licence.ToBinary();

            // GIVE LICENCE TO CLIENT IN BINARY FORM (XML IS ALSO SUPPORTED)

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportParameters(publicKey);

                // Parse the binary licence
                Licence clientLicence = Licence.FromBinary(binaryLicence);

                // Validate that the licence is still valid.
                ValidationResult result = clientLicence.Validate(rsa);

                if (result == ValidationResult.Valid)
                {
                    Console.WriteLine("Valid!");
                }
                else
                {
                    Console.WriteLine("Invalid + " + result);
                }
            }

            Console.Read();
        }
    }
}
