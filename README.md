# LicencingNET
LicencingNET is a lightweight, cross platform and dependency free library for .NET that allows you to easily implement software licencing and product registration into your applications. It contains methods for generating, signing and validating licences. LicencingNET works by creating a licence with certain grants and then signing it cryptographically to prevent tamper.

## Features
* Allows you to add ANY custom claims to a licence (for example, trial licences  or licences that just unlock SOME features)
* Supports Start and Expiration dates
* Supports .NET 2.0 and above
* Supports .NET Standard 2.0
* Dependency free
* Tamper proof licences
* Backwards compatible licence format (Both binary and XML)
* RSA and DSA support
* Supports RSA/DSA keys or RSA/DSA certificate / PFXs
* Tiny implementation to reduce attack surface (less than 100 lines for signing and verifying)
* Uses NTP by default to counter clock change attacks (optional)


## Usage
To create and validate licences. You need a RSA or DSA key pair. Either you can get this by generating a pair below, or by using certificates. This only has to be done once.
### Generate Keys

```csharp
using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
{
    RSAParameters privateKey = rsa.ExportParameters(true);
    RSAParameters publicKey = rsa.ExportParameters(false);

    XmlSerializer serializer = new XmlSerializer(typeof(RSAParameters));

    using (StringWriter publicKeyWriter = new StringWriter())
    using (StringWriter privateKeyWriter = new StringWriter())
    {
        serializer.Serialize(privateKeyWriter, privateKey);
        serializer.Serialize(publicKeyWriter, publicKey);

        // Store this public key in all programs that has to validate a licence. 
        // This is safe to distribute to clients.
        string publicKeyXml = publicKeyWriter.ToString();
        // Store this private key on your server where you want to create licences.
        // This is secret! If anyone gets hold of it, they can create as many licences as they like.
        string privateKeyXml = privateKeyWriter.ToString();
    }
}
```

Now that you have the keys saved as XML. Before you use them with LicencingNET, they have to be turned back into RSAParameters. This can be done like this:

```csharp
using (StringReader keyReader = new StringReader(xmlString))
{
    XmlSerializer serializer = new XmlSerializer(typeof(RSAParameters));
    RSAParameters key = (RSAParameters)serializer.Deserialize(keyReader);
}
```

### Creating a licence
First, you need to create a licence. Below is an example of how that can be done. This can ONLY be done on your server where you can keep the private key secret.

```csharp
// Creates a trial licence that is valid from now, and for 30 days with information about the receiver of the licence.
Licence licence = Licence.Create(null, DateTime.Now, DateTime.Now.AddDays(30), new Dictionary<string, string>()
{
    { "LicenceType", "Trial" },
    { "CustomerName", "John Doe" },
    { "CustomerEmail", "john.doe@contoso.com" },
    { "CustomerCompany", "Contoso Ltd." }
});
```

### Signing the licence
After it has been created, it has to be signed to make it tamper proof. It can be done like this:

```csharp
// Make sure you use the private key here.
RSAParameters privateKey;
using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
{
    // Import the private key.
    rsa.ImportParameters(privateKey);
    // Sign the licence.
    bool success = licence.Sign(rsa);
}
```

### Exporting licences
Next, you can send it to your clients. Either in binary or in XML. It can be done like this:

```csharp
byte[] binaryLicence = licence.ToBinary();
```

```csharp
string xmlLicence = licence.ToXML();
```

### Importing licences
Next, in your application. Simply validate the licence and enable features based on the licence grants. Start by deserializing it:

```csharp
Licence licence = Licence.FromXML(xmlLicence);
```

```csharp
Licence licence = Licence.FromBinary(binaryLicence);
```

### Validating licences
Next, make sure the licence is valid. This requires the public key. This can be done like this:

```csharp
// Make sure you use the public key here.
RSAParameters publicKey;
using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
{
    // Import the public key.
    rsa.ImportParameters(publicKey);

    // Validate the licence
    ValidationResult result = licence.Validate(rsa);

    if (result == ValidationResult.Valid)
    {
        // Licence is valid!
        // You can now safely use the attributes that you created.
        // Example:
        if (licence.Attributes.TryGetValue("LicenceType", out string licenceType))
        {
            if (licenceType == "Trial")
            {
                // Activate trial features.
            }
            else if if (licenceType == "Full")
            {
                // Activate all features.
            }
        }
    }
    else
    {
        // Licence is not valid.
        Console.WriteLine("Invalid + " + failure);
    }
}
```