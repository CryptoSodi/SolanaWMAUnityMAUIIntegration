using System;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using System.Diagnostics;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public static class EcdsaSignatures
    {
        private const int EncodedPublicKeyLengthBytes = 65;
        private const int P256DerSignaturePrefixLen = 2; // 0x30 || 1-byte length
        private const byte P256DerSignaturePrefixType = (byte)0x30;
        private const int P256DerSignatureComponentPrefixLen = 2; // 0x02 || 1-byte length
        private const byte P256DerSignatureComponentPrefixType = (byte)0x02;
        private const int P256DerSignatureComponentMinLen = 1;
        private const int P256DerSignatureComponentMaxLen = 33;
        private const int P256P1363ComponentLen = 32;
        private const int P256P1363SignatureLen = 64;

        public static byte[] EncodeP256PublicKey(ECPublicKeyParameters ecPublicKey)
        {
            var w = ecPublicKey.Q;
            var x = w.AffineXCoord.GetEncoded();
            var y = w.AffineYCoord.GetEncoded();
            var encodedPublicKey = new byte[EncodedPublicKeyLengthBytes];
            encodedPublicKey[0] = 0x04;
            int xLen = Math.Min(x.Length, 32);
            int yLen = Math.Min(y.Length, 32);
            Array.Copy(x, x.Length - xLen, encodedPublicKey, 33 - xLen, xLen);
            Array.Copy(y, y.Length - yLen, encodedPublicKey, 65 - yLen, yLen);
            return encodedPublicKey;
        }

        public static byte[] ConvertEcp256SignatureDeRtoP1363(byte[] derSignature, int offset)
        {
            if ((offset + P256DerSignaturePrefixLen) > derSignature.Length)
            {
                throw new ArgumentException("DER signature buffer too short to define sequence");
            }

            byte derType = derSignature[offset];
            if (derType != P256DerSignaturePrefixType)
            {
                throw new ArgumentException("DER signature has invalid type");
            }

            int derSeqLen = derSignature[offset + 1];

            byte[] p1363Signature = new byte[P256P1363SignatureLen];
            int sOff = UnpackDerIntegerToP1363Component(derSignature,
                offset + P256DerSignaturePrefixLen, p1363Signature, 0);
            int totalOff = UnpackDerIntegerToP1363Component(derSignature, sOff, p1363Signature,
                P256P1363ComponentLen);

            if ((offset + P256DerSignaturePrefixLen + derSeqLen) != totalOff)
            {
                throw new ArgumentException("Invalid DER signature length");
            }

            return p1363Signature;
        }

        public static byte[] ConvertEcp256SignatureP1363ToDer(byte[] p1363Signature, int p1363Offset)
        {
            if ((p1363Offset + P256P1363SignatureLen) > p1363Signature.Length)
            {
                throw new Exception("Invalid P1363 signature length");
            }

            int rDerIntLen = CalculateDerIntLengthOfP1363Component(p1363Signature, p1363Offset);
            int sDerIntLen = CalculateDerIntLengthOfP1363Component(p1363Signature, p1363Offset + P256P1363ComponentLen);

            byte[] derSignature = new byte[P256DerSignaturePrefixLen +
                                           2 * P256DerSignatureComponentPrefixLen + rDerIntLen + sDerIntLen];
            derSignature[0] = P256DerSignaturePrefixType;
            derSignature[1] = (byte)(2 * P256DerSignatureComponentPrefixLen + rDerIntLen + sDerIntLen);
            int sOff = PackP1363ComponentToDerInteger(p1363Signature, p1363Offset, rDerIntLen,
                derSignature, P256DerSignaturePrefixLen);
            int totalLen = PackP1363ComponentToDerInteger(p1363Signature,
                p1363Offset + P256P1363ComponentLen, sDerIntLen, derSignature, sOff);
            Debug.Assert(totalLen == derSignature.Length);
            return derSignature;
        }

        private static int PackP1363ComponentToDerInteger(
            byte[] p1363Signature,
            int p1363Offset,
            int p1363ComponentDerIntLength,
            byte[] derSignature,
            int derOffset)
        {
            Debug.Assert(p1363ComponentDerIntLength > 1 && p1363ComponentDerIntLength <= P256P1363ComponentLen + 1);

            derSignature[derOffset] = P256DerSignatureComponentPrefixType;
            derSignature[derOffset + 1] = (byte)p1363ComponentDerIntLength;

            int leadingBytes = Math.Max(0, p1363ComponentDerIntLength - P256P1363ComponentLen);
            int copyLen = Math.Min(p1363ComponentDerIntLength, P256P1363ComponentLen);
            
            for(int i = 0; i < leadingBytes; i++)
                derSignature[derOffset + P256DerSignatureComponentPrefixLen + i] = 0;
                
            Array.Copy(p1363Signature, p1363Offset + P256P1363ComponentLen - copyLen,
                derSignature, derOffset + P256DerSignatureComponentPrefixLen + leadingBytes,
                copyLen);

            return derOffset + P256DerSignatureComponentPrefixLen + p1363ComponentDerIntLength;
        }

        private static int CalculateDerIntLengthOfP1363Component(byte[] p1363Signature, int p1363Offset)
        {
            byte val = p1363Signature[p1363Offset];
            if (val > 127)
            {
                return P256P1363ComponentLen + 1;
            }
            return P256P1363ComponentLen;
        }

        private static int UnpackDerIntegerToP1363Component(byte[] derSignature, int derOffset, byte[] p1363Signature, int p1363Offset)
        {
            if ((derOffset + P256DerSignatureComponentPrefixLen) > derSignature.Length)
            {
                throw new ArgumentOutOfRangeException("DER signature buffer too short to define component");
            }

            var componentDerType = derSignature[derOffset];
            int componentLen = derSignature[derOffset + 1];

            if (componentDerType != P256DerSignatureComponentPrefixType ||
                componentLen < P256DerSignatureComponentMinLen ||
                componentLen > P256DerSignatureComponentMaxLen)
            {
                throw new ArgumentException("DER signature component not well formed");
            }

            if ((derOffset + P256DerSignatureComponentPrefixLen + componentLen) >
                derSignature.Length)
            {
                throw new ArgumentException("DER signature component exceeds buffer length");
            }

            var copyLen = Math.Min(componentLen, P256P1363ComponentLen);

            var srcOffset = derOffset + P256DerSignatureComponentPrefixLen +
                componentLen - copyLen;
            var dstOffset = p1363Offset + P256P1363ComponentLen - copyLen;
            
            for(int i = 0; i < P256P1363ComponentLen - copyLen; i++)
                p1363Signature[p1363Offset + i] = 0;
                
            Array.Copy(derSignature, srcOffset, p1363Signature, dstOffset, copyLen);
            return derOffset + P256DerSignatureComponentPrefixLen + componentLen;
        }

        public static ECPublicKeyParameters DecodeP256PublicKey(byte[] encodedPublicKey)
        {
            if (encodedPublicKey.Length < EncodedPublicKeyLengthBytes || encodedPublicKey[0] != 0x04)
            {
                throw new ArgumentException("input is not an EC P-256 public key");
            }

            byte[] x = new byte[32];
            byte[] y = new byte[32];
            Array.Copy(encodedPublicKey, 1, x, 0, 32);
            Array.Copy(encodedPublicKey, 33, y, 0, 32);

            X9ECParameters ecP = SecNamedCurves.GetByName("secp256r1");
            ECDomainParameters ecSpec = new ECDomainParameters(ecP.Curve, ecP.G, ecP.N, ecP.H);
            BigInteger xBig = new BigInteger(1, x);
            BigInteger yBig = new BigInteger(1, y);

            ECPoint w = ecP.Curve.CreatePoint(xBig, yBig);
            return new ECPublicKeyParameters(w, ecSpec);
        }
    }
}
