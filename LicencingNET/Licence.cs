using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace LicencingNET
{
    /// <summary>
    /// Represents a software licence.
    /// </summary>
    public class Licence
    {
        /// <summary>
        /// Serial number for the licence. Should be unique.
        /// </summary>
        /// <value>The serial number of the licence.</value>
        public Guid Serial { get; internal set; }
        /// <summary>
        /// NotBefore date specifies the start date of the licence validity period.
        /// If null, it has no start.
        /// </summary>
        /// <value>The not before date.</value>
        public DateTime? NotBefore { get; internal set; }
        /// <summary>
        /// NotAfter date specifies the end date of the licence validity period.
        /// If null, it has no end.
        /// </summary>
        /// <value>The not after date.</value>
        public DateTime? NotAfter { get; internal set; }
        /// <summary>
        /// Custom attributes of the licence.
        /// This could contain information about who its valid for, or what features/products the licence unlocks.
        /// </summary>
        /// <value>The licence attributes.</value>
        public Dictionary<string, string> Attributes { get; internal set; }
        /// <summary>
        /// Gets the signature for the licence.
        /// </summary>
        /// <value>The signature of the licence.</value>
        public byte[] Signature { get; internal set; }
        /// <summary>
        /// Gets whether or not the licence is signed.
        /// </summary>
        /// <value><c>true</c> if is signed; otherwise, <c>false</c>.</value>
        public bool HasSignature => Signature != null;

        private Licence()
        {

        }

        /// <summary>
        /// Gets the licence in binary format.
        /// </summary>
        /// <returns>The binary encoded licence.</returns>
        public byte[] ToBinary() => ToBinary(true);
        /// <summary>
        /// Constructs a licence object from a binary encoded licence.
        /// </summary>
        /// <returns>The decoded licence.</returns>
        /// <param name="binary">The binary encoded licence.</param>
        public static Licence FromBinary(byte[] binary) => FromBinary(binary, true);

        /// <summary>
        /// Gets the licence in xml format.
        /// </summary>
        /// <returns>The xml encoded licence.</returns>
        public string ToXML() => ToXML(true);
        /// <summary>
        /// Constructs a licence object from a xml encoded licence.
        /// </summary>
        /// <returns>The decoded licence.</returns>
        /// <param name="xml">The xml encoded licence.</param>
        public static Licence FromXML(string xml) => FromXML(xml, true);

        /// <summary>
        /// Creates a licence with a optional Serial, NotBefore date, NotAfter date and licence attributes.
        /// </summary>
        /// <returns>The created licence.</returns>
        /// <param name="serial">The serial for the licence. If null, it defaults to Guid.NewGuid().</param>
        /// <param name="notBefore">The start date for the licence. If null, it will have no start date.</param>
        /// <param name="notAfter">The end date for the licence. If null, it will have no end date<param>
        /// <param name="attributes">The custom licence attributes.</param>
        public static Licence Create(Guid? serial, DateTime? notBefore, DateTime? notAfter, Dictionary<string, string> attributes)
        {
            return new Licence()
            {
                Serial = serial != null ? serial.Value : Guid.NewGuid(),
                NotBefore = notBefore,
                NotAfter = notAfter,
                Attributes = attributes != null ? attributes : new Dictionary<string, string>(),
                Signature = null
            };
        }

        /// <summary>
        /// Modifies all properties the and clears the signature.
        /// </summary>
        /// <param name="serial">The new Serial.</param>
        /// <param name="notBefore">The new NotBefore date.</param>
        /// <param name="notAfter">The new NotAfter date.</param>
        /// <param name="attributes">The new Attributes.</param>
        public void ModifyAndClearSignature(Guid? serial, DateTime? notBefore, DateTime? notAfter, Dictionary<string, string> attributes)
        {
            Signature = null;
            Serial = serial != null ? serial.Value : Guid.NewGuid();
            NotBefore = notBefore;
            NotAfter = notAfter;
            Attributes = attributes != null ? attributes : new Dictionary<string, string>();
        }

        /// <summary>
        /// Validate the licence using the specified certificate.
        /// </summary>
        /// <returns>The validation status.</returns>
        /// <param name="certificate">The certificate used to validate the licence.</param>
        public ValidationResult Validate(X509Certificate2 certificate, bool useNtp = true) => Validate(certificate.PublicKey.Key, useNtp);

        /// <summary>
        /// Validate the licence using the specified RSA or DSA public key.
        /// </summary>
        /// <returns>The validation status.</returns>
        /// <param name="publicKey">The public RSA or DSA key used to validate the licence.</param>
        public ValidationResult Validate(AsymmetricAlgorithm publicKey, bool useNtp = true)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey), "Public key cannot be null");
            }

            if (!HasSignature)
            {
                return ValidationResult.NoSignature;
            }

            DateTime currentTime = DateTime.UtcNow;
            try
            {
                if (useNtp)
                {
                    currentTime = NTP.GetNetworkTime();
                }
            }
            catch
            {
            }

            if (NotAfter != null && currentTime > NotAfter.Value.ToUniversalTime())
            {
                return ValidationResult.Exipired;
            }

            if (NotBefore != null && currentTime < NotBefore.Value.ToUniversalTime())
            {
                return ValidationResult.NotStarted;
            }

            using (SHA512 sha = SHA512.Create())
            {
                byte[] licenceBinary = ToBinary(false);

                if (publicKey is RSACryptoServiceProvider rsa)
                {
                    if (rsa.VerifyData(licenceBinary, sha, Signature))
                    {
                        return ValidationResult.Valid;
                    }
                    else
                    {
                        return ValidationResult.InvalidSignature;
                    }
                }

                if (publicKey is DSACryptoServiceProvider dsa)
                {
                    if (dsa.VerifySignature(sha.ComputeHash(licenceBinary), Signature))
                    {
                        return ValidationResult.Valid;
                    }
                    else
                    {
                        return ValidationResult.InvalidSignature;
                    }
                }

                throw new NotSupportedException("Only RSA and DSA signatures are supported");
            }
        }

        /// <summary>
        /// Sign the licence with the specified certificates private RSA or DSA key.
        /// </summary>
        /// <returns>Whether the signature was successfully created.</returns>
        /// <param name="certificate">The certificate containing a private RSA or DSA key.</param>
        public bool Sign(X509Certificate2 certificate) => Sign(certificate.PrivateKey);

        /// <summary>
        /// Sign the licence with the specified private RSA or DSA key.
        /// </summary>
        /// <returns>Whether the signature was successfully created.</returns>
        /// <param name="privateKey">The private RSA or DSA key to use.</param>
        public bool Sign(AsymmetricAlgorithm privateKey)
        {
            if (privateKey == null)
            {
                throw new ArgumentNullException(nameof(privateKey), "Private key cannot be null");
            }

            using (SHA512 sha = SHA512.Create())
            {
                byte[] licenceBinary = ToBinary(false);

                if (privateKey is RSACryptoServiceProvider rsa && !rsa.PublicOnly)
                {
                    byte[] signature = rsa.SignData(licenceBinary, sha);

                    Signature = signature;

                    return true;
                }

                if (privateKey is DSACryptoServiceProvider dsa && !dsa.PublicOnly)
                {
                    byte[] licenceHash = sha.ComputeHash(licenceBinary);
                    byte[] signature = dsa.CreateSignature(licenceHash);

                    Signature = signature;

                    return true;
                }
            }

            return false;
        }

        internal static Licence FromBinary(byte[] binary, bool importSignature)
        {
            if (binary == null)
            {
                throw new ArgumentNullException(nameof(binary), "Binary input cannot be null");
            }

            using (MemoryStream stream = new MemoryStream(binary))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    Licence licence = new Licence();

                    ushort version = reader.ReadUInt16();
                    if (!Constants.SUPPORTED_VERSIONS.Contains(version))
                    {
                        throw new FormatException("Version " + version + " is not supported");
                    }

                    byte serialLength = reader.ReadByte();
                    licence.Serial = new Guid(reader.ReadBytes(serialLength));

                    if (reader.ReadBoolean())
                    {
                        licence.NotBefore = DateTime.FromBinary(reader.ReadInt64());
                    }
                    else
                    {
                        licence.NotBefore = null;
                    }

                    if (reader.ReadBoolean())
                    {
                        licence.NotAfter = DateTime.FromBinary(reader.ReadInt64());
                    }
                    else
                    {
                        licence.NotAfter = null;
                    }

                    ushort attributeCount = reader.ReadUInt16();
                    licence.Attributes = new Dictionary<string, string>();
                    for (int i = 0; i < attributeCount; i++)
                    {
                        licence.Attributes.Add(reader.ReadString(), reader.ReadString());
                    }

                    if (importSignature)
                    {
                        ushort signatureLength = reader.ReadUInt16();
                        byte[] signature = signatureLength == 0 ? null : reader.ReadBytes(signatureLength);

                        licence.Signature = signature;
                    }

                    return licence;
                }
            }
        }

        internal static Licence FromXML(string xml, bool importSignature)
        {
            if (string.IsNullOrEmpty(xml) || string.IsNullOrEmpty(xml.Trim()))
            {
                throw new ArgumentNullException(nameof(xml), "Xml input cannot be null or empty");
            }

            using (StringReader stringReader = new StringReader(xml))
            {
                using (XmlReader reader = XmlReader.Create(stringReader))
                {
                    Licence licence = new Licence();

                    if (!reader.ReadToFollowing("Licence"))
                    {
                        throw new FormatException("Licence tag not found");
                    }

                    string versionString = reader.GetAttribute("Version");
                    if (ushort.TryParse(versionString, out ushort version))
                    {
                        if (!Constants.SUPPORTED_VERSIONS.Contains(version))
                        {
                            throw new FormatException("Version " + version + " is not supported");
                        }
                    }
                    else
                    {
                        throw new FormatException("Invalid Version format");
                    }

                    reader.ReadStartElement("Licence");

                    string serialString = reader.ReadElementContentAsString("Serial", "");
                    try
                    {
                        Guid serial = new Guid(serialString);

                        licence.Serial = serial;
                    }
                    catch (Exception e)
                    {
                        throw new FormatException("Invalid Serial format", e);
                    }

                    string notBeforeString = reader.ReadElementContentAsString("NotBefore", "");
                    if (notBeforeString.ToLower() == "n/a")
                    {
                        licence.NotBefore = null;
                    }
                    else
                    {
                        if (long.TryParse(notBeforeString, out long notBeforeLong))
                        {
                            licence.NotBefore = DateTime.FromBinary(notBeforeLong);
                        }
                        else
                        {
                            throw new FormatException("Invalid NotBefore format");
                        }
                    }


                    string notAfterString = reader.ReadElementContentAsString("NotAfter", "");
                    if (notAfterString.ToLower() == "n/a")
                    {
                        licence.NotAfter = null;
                    }
                    else
                    {
                        if (long.TryParse(notAfterString, out long notAfterLong))
                        {
                            licence.NotAfter = DateTime.FromBinary(notAfterLong);
                        }
                        else
                        {
                            throw new FormatException("Invalid NotAfter format");
                        }
                    }

                    Dictionary<string, string> attributes = new Dictionary<string, string>();

                    reader.ReadStartElement("Attributes");
                    while (reader.NodeType == XmlNodeType.Element && reader.Name == "Attribute" && reader.Read())
                    {
                        string key = reader.ReadElementContentAsString("Key", "");
                        string value = reader.ReadElementContentAsString("Value", "");

                        attributes.Add(key, value);

                        while (true)
                        {
                            bool fail = reader.NodeType != XmlNodeType.EndElement || reader.Name != "Attribute";

                            if (!reader.Read())
                            {
                                throw new FormatException("Could not find Attribute end element");
                            }

                            if (!fail)
                            {
                                break;
                            }
                        }
                    }

                    while (true)
                    {
                        bool fail = reader.NodeType != XmlNodeType.EndElement || reader.Name != "Attributes";

                        if (!reader.Read())
                        {
                            throw new FormatException("Could not find Attributes end element");
                        }

                        if (!fail)
                        {
                            break;
                        }
                    }

                    licence.Attributes = attributes;

                    if (importSignature)
                    {
                        string signatureString = reader.ReadElementContentAsString("Signature", "");
                        try
                        {
                            byte[] signatureBytes = Convert.FromBase64String(signatureString);
                            licence.Signature = signatureBytes;
                        }
                        catch (Exception e)
                        {
                            licence.Signature = null;
                            throw new FormatException("Invalid signature", e);
                        }
                    }
                    else
                    {
                        licence.Signature = null;
                    }

                    if (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "Licence"))
                    {
                        throw new FormatException("No licence end tag found");
                    }

                    return licence;
                }
            }
        }

        internal string ToXML(bool exportSignature)
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(stringWriter))
                {
                    writer.WriteStartElement("Licence");
                    writer.WriteAttributeString("Version", Constants.CURRENT_VERSION.ToString());
                    writer.WriteElementString("Serial", Serial.ToString());
                    writer.WriteElementString("NotBefore", (NotBefore == null) ? "N/A" : NotBefore.Value.ToBinary().ToString());
                    writer.WriteElementString("NotAfter", (NotAfter == null) ? "N/A" : NotAfter.Value.ToBinary().ToString());

                    writer.WriteStartElement("Attributes");
                    List<KeyValuePair<string, string>> sortedAttributes = Attributes.Select(x => x).OrderBy(x => x.Key).ToList();

                    foreach (KeyValuePair<string, string> attribute in sortedAttributes)
                    {
                        writer.WriteStartElement("Attribute");
                        writer.WriteElementString("Key", attribute.Key);
                        writer.WriteElementString("Value", attribute.Value);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();

                    if (exportSignature)
                    {
                        writer.WriteElementString("Signature", Signature == null ? "" : Convert.ToBase64String(Signature));
                    }

                    writer.WriteEndElement();
                    writer.Flush();
                }

                return stringWriter.ToString();
            }
        }

        internal byte[] ToBinary(bool exportSignature)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    byte[] serialBinary = Serial.ToByteArray();

                    if (serialBinary.Length > byte.MaxValue)
                    {
                        throw new InvalidOperationException("Invalid serial");
                    }

                    writer.Write(Constants.CURRENT_VERSION);

                    writer.Write((byte)serialBinary.Length);
                    writer.Write(serialBinary);

                    writer.Write(NotBefore != null);
                    if (NotBefore != null)
                    {
                        writer.Write(NotBefore.Value.ToBinary());
                    }

                    writer.Write(NotAfter != null);
                    if (NotAfter != null)
                    {
                        writer.Write(NotAfter.Value.ToBinary());
                    }

                    if (Attributes.Count > ushort.MaxValue)
                    {
                        throw new InvalidOperationException("Too many attributes");
                    }

                    List<KeyValuePair<string, string>> sortedAttributes = Attributes.Select(x => x).OrderBy(x => x.Key).ToList();

                    writer.Write((ushort)sortedAttributes.Count);
                    foreach (KeyValuePair<string, string> attribute in sortedAttributes)
                    {
                        writer.Write(attribute.Key);
                        writer.Write(attribute.Value);
                    }

                    if (exportSignature)
                    {
                        if (Signature == null)
                        {
                            writer.Write((ushort)0);
                        }
                        else
                        {
                            writer.Write((ushort)Signature.Length);
                            writer.Write(Signature);
                        }
                    }
                }

                return stream.ToArray();
            }
        }
    }
}
