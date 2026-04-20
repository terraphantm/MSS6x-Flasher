using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace MSS6x_Flasher
{
    class Checksums_Signatures
    {
        public byte[] GetSecurityAccessMessage(byte[] userID, byte[] serialNumber, byte[] seed, byte securityAccesslevel)
        {
            BigInteger n = 1;
            BigInteger d = 0;

            if (securityAccesslevel == 3)
            {
                if (Global.HW_Ref == "0569Q60")
                {
                    n = BigInteger.Parse("8696140873888434446387975732326763523249545954768330639267165602855312564078476997195475125527812090097980466687030439806610464985599164179002751305271403");
                    d = BigInteger.Parse("7453835034761515239761122056280083019928182246944283405086141945304553626352816724383157056945283320916196838651674180905727480578257637388246812114390247");
                }

                if (Global.HW_Ref == "0569QT0")
                {
                    n = BigInteger.Parse("8872204441036755971493396648972430644566544772201619424097935037771336781905258690489188917401812198294877658309097685765054576652302159306784660331677077");
                    d = BigInteger.Parse("2534915554581930277569541899706408755590441363486176978313695725077524794830019003951231561805102167208019053855272364025197896095040928732790152893329463");
                }
            }
            if (securityAccesslevel == 4 || securityAccesslevel == 5)
            {
                if (Global.HW_Ref == "0569Q60")
                {
                    n = BigInteger.Parse("8253757306003757868525777640169718604932757000074920874994222231950868821969719190712750985526407274522827192794874008844597111915352953813986651252706633");
                    d = BigInteger.Parse("7074649119431792458736380834431187375656648857207075035709333341672173275973885995701180450272895466422858788882048044419385699526129917451317091936651703");
                }

                if (Global.HW_Ref == "0569QT0")
                {
                    n = BigInteger.Parse("8602771258417735749409814056345908435532661082027266916548055133294227378955678061118286524160442955777175472033945102551026362228399822502680246206318147");
                    d = BigInteger.Parse("4915869290524420428234179460769090534590092046872723952313174361882415645117422898322886570890084852966859241876825067830373967643297871614608359208031223");
                }
            }
            /*if (securityAccesslevel == 5)
            {
                    n = BigInteger.Parse("8209836131236623540106248032442393614982637631141339598467747244677911494877848116936777629774248859738700738214196985237506543807286694994580847683477459");
                    d = BigInteger.Parse("5864168665169016814361605737458852582130455450815242570334105174769936782055474121615341083788171349392703586501785350322243672467184174299268442897849743");
            }*/


            byte[] toHash = userID.Concat(serialNumber.Concat(seed)).ToArray(); //Hash of UserID + Serial Number + Random number = authentication message

            MD5 md5hash = MD5.Create();
            byte[] hash = new byte[16];
            hash = md5hash.ComputeHash(toHash); //generate MD5 

            BigInteger ToEncrypt = new BigInteger(Append0(hash)); //Need to add a leading zero to hash so that we don't run into +/- issues
            BigInteger Encrypted = BigInteger.ModPow(ToEncrypt, d, n); //RSA encrypt the result (message ^ private exponent % modulus)
            byte[] encryptedArray = new Byte[64];
            encryptedArray = Encrypted.ToByteArray(); //Store result in array
            byte[] authPayload = new Byte[65]; //Need to swap endianness
            authPayload[64] = 3;

            for (int i = 0; i < 16; ++i)
            {
                authPayload[0 + 4 * (i)] = encryptedArray[3 + 4 * i];
                authPayload[1 + 4 * (i)] = encryptedArray[2 + 4 * i];
                authPayload[2 + 4 * (i)] = encryptedArray[1 + 4 * i];
                authPayload[3 + 4 * (i)] = encryptedArray[0 + 4 * i];
            } //Probably a more elegant way to do this




            byte[] authHeader = { 01, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 0x44, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 0x10 }; //Ediabas wants to see this header -- full meaning can be seen in those comments
            byte[] authMessage = authHeader.Concat(authPayload).ToArray();
            
            return authMessage;
        }

        public byte[] CorrectParameterChecksums(byte[] DataToFlash)
        {

            uint numberOfSegments = BitConverter.ToUInt32(DataToFlash.Skip(0x1C0).Take(4).Reverse().ToArray(), 0);
            uint checksumStart = 0;
            uint checksumEnd = 0;
            uint crc = 0xFFFFFFFF;
            uint maxAddress = 0x7FFFB; //This is hardcoded in the DME
            uint MemSubtract = 0x70000;

            for (int i = 0; i < numberOfSegments; ++i)
            {
                checksumStart = BitConverter.ToUInt32(DataToFlash.Skip(0x1C4 + (8 * i)).Take(4).Reverse().ToArray(), 0) - MemSubtract;

                checksumEnd = BitConverter.ToUInt32(DataToFlash.Skip(0x1C8 + (8 * i)).Take(4).Reverse().ToArray(), 0) - MemSubtract;
                if (checksumEnd > maxAddress - MemSubtract)
                    checksumEnd = maxAddress - MemSubtract;
                //Console.WriteLine(checksumStart.ToString("x"));
                //Console.WriteLine(checksumEnd.ToString("x"));

                crc = CalculateCRC32(DataToFlash.Skip((int)checksumStart).Take((int)(checksumEnd - checksumStart) + 1).ToArray(), crc);
            }
            crc = ~crc;

            byte[] CS_calc_array = BitConverter.GetBytes(crc).ToArray();
            //Console.WriteLine(BitConverter.ToString(CS_calc_array));
            for (int i = 0; i < 4; ++i)
                DataToFlash[0xFFFC + i] = CS_calc_array[3 - i];

            for (int i = 0x80; i < 0xC0; ++i)
                DataToFlash[i] = 0xFF;

            return DataToFlash;

        }

        public byte[] CorrectProgramChecksums(byte[] DataToFlash)
        {

            uint numberOfSegments = BitConverter.ToUInt32(DataToFlash.Skip(0x101C0).Take(4).Reverse().ToArray(), 0);
            uint checksumStart = 0;
            uint checksumEnd = 0;
            uint crc = 0xFFFFFFFF;
            uint maxAddress = BitConverter.ToUInt32(DataToFlash.Skip(0x1001C).Take(4).Reverse().ToArray(), 0) + 3;
            uint MemSubtract = 0x380000;

            for (int i = 0; i < numberOfSegments; ++i)
            {
                checksumStart = BitConverter.ToUInt32(DataToFlash.Skip(0x101C4 + (8 * i)).Take(4).Reverse().ToArray(), 0);
                checksumEnd = BitConverter.ToUInt32(DataToFlash.Skip(0x101C8 + (8 * i)).Take(4).Reverse().ToArray(), 0);

                if (checksumEnd > maxAddress)
                    checksumEnd = maxAddress;

                if (checksumStart >= 0x450000)
                {
                    checksumStart = checksumStart - MemSubtract;
                    checksumEnd = checksumEnd - MemSubtract;
                }

                /*//Console.WriteLine(checksumStart.ToString("x"));
                //Console.WriteLine(checksumEnd.ToString("x"));*/

                crc = CalculateCRC32(DataToFlash.Skip((int)checksumStart).Take((int)(checksumEnd - checksumStart) + 1).ToArray(), crc);
            }

            crc = ~crc;

            byte[] CS_calc_array = BitConverter.GetBytes(crc).ToArray();
            //Console.WriteLine(BitConverter.ToString(CS_calc_array));
            for (int i = 0; i < 4; ++i)
                DataToFlash[maxAddress - MemSubtract + 1 + i] = CS_calc_array[3 - i];

            for (int i = 0; i < 0xC0; ++i)
                DataToFlash[0x10040 + i] = 0xFF;

            return DataToFlash;

        }

        public byte[] PatchProgram(byte[] binary)
        {
            byte[] binary_patched = binary;

            /* At this point, the DME is checking if the signature is a valid length or not.
             * The patch modifies the code to always return a 0 (== passed) instead of -1 (fail).
             * Lots of possible ways to accomplish the same net effect. 
             */
            byte[] rsa_check = { 0x81, 0x86, 0x00, 0x00, 0x28, 0x0C, 0x00, 0x20, 0x40, 0x81, 0x00, 0x0C, 0x38, 0x60, 0xFF, 0xFF };
            //byte[] rsa_patch = { 0x81, 0x86, 0x00, 0x00, 0x28, 0x0C, 0x00, 0x00, 0x41, 0x80, 0x00, 0x0C, 0x38, 0x60, 0x00, 0x00 };
            byte[] rsa_patch = { 0x81, 0x86, 0x00, 0x00, 0x28, 0x0C, 0x00, 0x20, 0x60, 0x00, 0x00, 0x00, 0x38, 0x60, 0x00, 0x00 };

            int indexOfRSASequence = SearchBytes(binary, rsa_check);
            if (indexOfRSASequence != -1)
            {
                for (int i = 0; i < rsa_patch.Length; ++i)
                    binary_patched[indexOfRSASequence + i] = rsa_patch[i];
                Console.WriteLine("RSA Patched @ 0x" + indexOfRSASequence.ToString("x"));
            }

            //If an AIF is programmed into the DME and you have a program flashed with an empty tune, the DME will refuse to erase the program until a tune is flashed
            //This is why I initially had to write a tune before doing a proper RSA patch
            //Now I patch out the AIF check at the same time as the RSA bypass itself. Overall makes the whole bypass process faster
            byte[] EraseRoutine_AIF_Check = {0x2C, 0x03, 0x00, 0x00, 0x40, 0x82, 0x00, 0x1C, 0x3B, 0xA0, 0x00, 0x08 };
            byte[] EraseRoutine_AIF_Patch = { 0x2C, 0x03, 0x00, 0x00, 0x48, 0x00, 0x00, 0x1C, 0x3B, 0xA0, 0x00, 0x08 };

            int indexOfEraseAIFSequence = SearchBytes(binary, EraseRoutine_AIF_Check);
            if (indexOfEraseAIFSequence != -1)
            {
                for (int i = 0; i < EraseRoutine_AIF_Check.Length; ++i)
                    binary_patched[indexOfEraseAIFSequence + i] = EraseRoutine_AIF_Patch[i];
                Console.WriteLine("Erase AIF Check Patched @ 0x" + indexOfEraseAIFSequence.ToString("x"));
            }

            // Changes a return value from 04 to 03 (DME will send the buffer if anything other than 0 or 04, will send an FF or 00 if 04).
            //byte[] read_protect = { 0x48, 0x00, 0x00, 0x10, 0x38 ,0x60, 0x00, 0x04 }; 
            //byte[] read_protect_patch = { 0x48, 0x00, 0x00, 0x10, 0x38, 0x60, 0x00, 0x03 };

            //Need to check whether this patch causes issues when trying to read a non-readable address. 
            byte[] read_protect = { 0x7c, 0x03, 0x60, 0x40, 0x40, 0x82, 0x00, 0x0c, 0x38, 0x60, 0x00, 0x05 }; 
            byte[] read_protect_patch = { 0x7c, 0x03, 0x60, 0x40, 0x60, 0x00, 0x00, 0x00, 0x38, 0x60, 0x00, 0x05 };
            
            int indexOfReadProtectSeqeuence = SearchBytes(binary, read_protect);
            if (indexOfReadProtectSeqeuence != -1)
            {
                for (int i = 0; i < read_protect_patch.Length; ++i)
                    binary_patched[indexOfReadProtectSeqeuence + i] = read_protect_patch[i];
                Console.WriteLine("Read Protection Patched @ 0x" + indexOfReadProtectSeqeuence.ToString("x"));
            }

            //This set censor routine only exists on MSS60. It is called when you lock an EWS4 SK -- after it is done, the CPU cannot be read over BDM without clearing the censor (which wipes the flash)
            //Both injection and ignition do have this code, though only ever called on injection side. The patch makes it so the DME skips the routine and returns 0
            //This patch will not clear the censor if it is already set (i.e most MSS60s). It will only prevent it from being set
            byte[] SetUC3FCensor_Routine = { 0x81, 0x3f, 0xc8, 0x00, 0x3d, 0x80, 0x03, 0x00, 0x55, 0x29, 0x01, 0x8e, 0x7c, 0x09, 0x60, 0x40, 0x41, 0x82, 0x01, 0x30 };
            byte[] SetUC3FCensor_Patch = { 0x81, 0x3f, 0xc8, 0x00, 0x3d, 0x80, 0x03, 0x00, 0x55, 0x29, 0x01, 0x8e, 0x7c, 0x09, 0x60, 0x40, 0x48, 0x00, 0x01, 0x30 };

            int indexOfCensorSequence = SearchBytes(binary, SetUC3FCensor_Routine);
            if (indexOfCensorSequence != -1)
            {
                for (int i = 0; i < SetUC3FCensor_Patch.Length; ++i)
                    binary_patched[indexOfCensorSequence + i] = SetUC3FCensor_Patch[i];
                Console.WriteLine("UC3F Censor Routine Patched @ 0x" + indexOfCensorSequence.ToString("x"));
            }

            /*This copies the RSA pointers from 10204/70204 to 101c0/701c0 -- this will cause the RSA check to be done on preexisting boot code rather than new code
             *This allows us to pass the check and copy over our patched boot sector without actually validating any new data
             *This is not needed when flashing an already RSA bypassed DME, but these bytes are how I (and other flashing programs) detect whether an RSA bypass is present, so I write them anytime I patch the program
             */
            for (int i = 0; i < 0x2C; ++i)
                binary_patched[0x10204 + i] = binary_patched[0x101c0 + i];

            //Erase the affe0815 'rsa passed' sequence 
            for (int i = 0; i < 0x40; ++i)
                binary_patched[0x10080 + i] = 0xFF;

            return binary_patched;
        }

        public bool IsParamSignatureValid(byte[] binary)
        {
            byte[] Signature_Injection = binary.Skip(0x104).Take(0x80).ToArray();
            byte[] CalculatedMD5_Injection = CheckParamMD5(binary.Take(0x10000).ToArray());
            //Console.WriteLine("Calculated: " + BitConverter.ToString(CalculatedMD5_Injection));
            byte[] DecryptedMD5_Injection = DecryptSignature(Signature_Injection);
            //Console.WriteLine("Decrypted: " + BitConverter.ToString(DecryptedMD5_Injection));
            bool Injection_Match = CalculatedMD5_Injection.SequenceEqual(DecryptedMD5_Injection);
            //Console.WriteLine(Injection_Match);

            byte[] Signature_Ignition = binary.Skip(0x10104).Take(0x80).ToArray();
            byte[] CalculatedMD5_Ignition = CheckParamMD5(binary.Skip(0x10000).Take(0x10000).ToArray());
            //Console.WriteLine("Calculated: " + BitConverter.ToString(CalculatedMD5_Ignition));
            byte[] DecryptedMD5_Ignition = DecryptSignature(Signature_Ignition);
            //Console.WriteLine("Decrypted: " + BitConverter.ToString(DecryptedMD5_Ignition));
            bool Ignition_Match = CalculatedMD5_Ignition.SequenceEqual(DecryptedMD5_Ignition);
            //Console.WriteLine(Ignition_Match);
            return (Injection_Match && Ignition_Match);
        }

        public bool IsProgramSignatureValid(byte[] binary)
        {
            byte[] Signature_Injection = binary.Skip(0x10104).Take(0x80).ToArray();
            byte[] CalculatedMD5_Injection = CheckProgramMD5(binary.Take(0x280000).ToArray());
            //Console.WriteLine("Calculated: " + BitConverter.ToString(CalculatedMD5_Injection));
            byte[] DecryptedMD5_Injection = DecryptSignature(Signature_Injection);
            //Console.WriteLine("Decrypted: " + BitConverter.ToString(DecryptedMD5_Injection));
            bool Injection_Match = CalculatedMD5_Injection.SequenceEqual(DecryptedMD5_Injection);
            //Console.WriteLine(Injection_Match);

            byte[] Signature_Ignition = binary.Skip(0x290104).Take(0x80).ToArray();
            byte[] CalculatedMD5_Ignition = CheckProgramMD5(binary.Skip(0x280000).Take(0x280000).ToArray());
            //Console.WriteLine("Calculated: " + BitConverter.ToString(CalculatedMD5_Ignition));
            byte[] DecryptedMD5_Ignition = DecryptSignature(Signature_Ignition);
            //Console.WriteLine("Decrypted: " + BitConverter.ToString(DecryptedMD5_Ignition));
            bool Ignition_Match = CalculatedMD5_Ignition.SequenceEqual(DecryptedMD5_Ignition);
            //Console.WriteLine(Ignition_Match);
            return (Injection_Match && Ignition_Match);
        }

        private byte[] CheckParamMD5(byte[] binary)
        {
            uint numberOfSegments = BitConverter.ToUInt32(binary.Skip(0x1C0).Take(4).Reverse().ToArray(), 0);
            uint segment_start = 0;
            uint segment_end = 0;
            uint MemSubtract = 0x70000;

            byte[] toHash = new byte[0];
            for (int i = 0; i < numberOfSegments; ++i)
            {
                segment_start = BitConverter.ToUInt32(binary.Skip(0x1C4 + (8 * i)).Take(4).Reverse().ToArray(), 0) - MemSubtract;
                segment_end = BitConverter.ToUInt32(binary.Skip(0x1C8 + (8 * i)).Take(4).Reverse().ToArray(), 0) - MemSubtract;
                //Console.WriteLine(checksumStart.ToString("x"));
                //Console.WriteLine(checksumEnd.ToString("x"));

                toHash = toHash.Concat(binary.Skip((int)segment_start).Take((int)(segment_end - segment_start) + 1)).ToArray();
            }

            MD5 md5hash = MD5.Create();
            byte[] hash = new byte[16];
            hash = md5hash.ComputeHash(toHash); //generate MD5 
            //If private key is ever factored or leaked, this hash can be signed to generate a valid signature

            return hash;
        }

        private byte[] CheckProgramMD5(byte[] binary)
        {
            uint numberOfSegments = BitConverter.ToUInt32(binary.Skip(0x101C0).Take(4).Reverse().ToArray(), 0);
            uint segment_start = 0;
            uint segment_end = 0;
            uint MemSubtract = 0x380000;

            byte[] toHash = new byte[0];
            for (int i = 0; i < numberOfSegments; ++i)
            {
                segment_start = BitConverter.ToUInt32(binary.Skip(0x101C4 + (8 * i)).Take(4).Reverse().ToArray(), 0);

                segment_end = BitConverter.ToUInt32(binary.Skip(0x101C8 + (8 * i)).Take(4).Reverse().ToArray(), 0);

                if (segment_start >= 0x450000)
                {
                    segment_start = segment_start - MemSubtract;
                    segment_end = segment_end - MemSubtract;
                }

                toHash = toHash.Concat(binary.Skip((int)segment_start).Take((int)(segment_end - segment_start) + 1)).ToArray();
            }

            MD5 md5hash = MD5.Create();
            byte[] hash = new byte[16];
            hash = md5hash.ComputeHash(toHash);

            return hash;
        }

        private byte[] DecryptSignature(byte[] Signature)
        {
            BigInteger n = 1;
            if (Global.HW_Ref == "0569Q60")
            {
                n = BigInteger.Parse("112821069661138377315645977154981754818872711926253513954324506501925985219019412578651128376416548224217522142353410732220016028299360336544291257377851034205695695238581306745471969610106200216032551948699692549703274834151266414339482674326328156914539053791993861051643608033403003484660081699180996047157");
            }
            
            if (Global.HW_Ref == "0569QT0")
            {
                if (Global.Prog_Vers_internal_uint == 530)
                {
					//v530 prototype uses a different signing key. I suspect other prototypes do as well, but can't confirm
                    //Worth implementing signing those binaries?
					n = BigInteger.Parse("114202887327969767912813185724984560286311767411013655718162781605186657344931388634900851134893589309575054620353736326969354268958541565106297759609905717273852190357233619336152002477267788219527276170504229835617938468677166803770215288630415351752605271247120739821211047806402971122312644277678970489071");
				}
				else
                {
                    n = BigInteger.Parse("114042781749945754155193684304062515265250817826233262485981219735727053774369690563600757146222783995046157841392053358295567986112209298210934101483936176969137584875063033908405034200263553238461435326052045462761398743874020899471819971230329912258328571253256856424399489516538996026252224471705119091253");
                }
            }
            int e = 3;


            byte[] SignatureReverseDWORD = new byte[0];

            for (int i = 0; i < 0x20; ++i)
            {
                SignatureReverseDWORD = SignatureReverseDWORD.Concat(Signature.Skip((0x20 - i - 1) * 4).Take(4)).ToArray();
            }
            byte[] SignatureReverse = SignatureReverseDWORD.Reverse().ToArray();
            SignatureReverse = Append0(SignatureReverse);
            BigInteger BigIntSignature = new BigInteger(SignatureReverse);
            BigInteger Decrypted = BigInteger.ModPow(BigIntSignature, e, n);
            byte[] DecryptedArray = Decrypted.ToByteArray();
            DecryptedArray = DecryptedArray.Take(16).ToArray();

            return DecryptedArray;
        }

        private static byte[] Append0(byte[] array) //Array to BigInt function needs a 0 appended to the result to ensure the value is interpreted as positive
        {
            byte[] appended = new byte[array.Length + 1];

            for (int i = 0; i < array.Length; ++i)
                appended[i] = array[i];

            return appended;
        }

        private uint CalculateCRC32(byte[] buffer, uint initial)
        {
            uint[] crc32_table =
            {
                 0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA,
                 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
                 0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988,
                 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
                 0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE,
                 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
                 0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC,
                 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
                 0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172,
                 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
                 0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940,
                 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
                 0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116,
                 0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
                 0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924,
                 0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
                 0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A,
                 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
                 0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818,
                 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
                 0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E,
                 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
                 0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C,
                 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
                 0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2,
                 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
                 0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0,
                 0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
                 0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086,
                 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
                 0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4,
                 0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
                 0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A,
                 0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
                 0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8,
                 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
                 0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE,
                 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
                 0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC,
                 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
                 0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252,
                 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
                 0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60,
                 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
                 0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236,
                 0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
                 0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04,
                 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
                 0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A,
                 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
                 0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38,
                 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
                 0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E,
                 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
                 0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C,
                 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
                 0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2,
                 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
                 0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0,
                 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
                 0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6,
                 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
                 0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94,
                 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
        };

            {
                uint crc32 = initial;

                for (int i = 0; i < buffer.Length; ++i)
                    crc32 = crc32_table[(crc32 ^ buffer[i]) & 0xFF] ^ (crc32 >> 8);

                return crc32;
            }
        }

        static int SearchBytes(byte[] haystack, byte[] needle)
        {
            var len = needle.Length;
            var limit = haystack.Length - len;
            for (var i = 0; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k]) break;
                }
                if (k == len) return i;
            }
            return -1;
        }
    }
}
