/* this is generated by nino */
using System.Runtime.CompilerServices;

namespace Nino.Benchmark.Models
{
    public partial class AccountMerge
    {
        public static AccountMerge.SerializationHelper NinoSerializationHelper = new AccountMerge.SerializationHelper();
        public unsafe class SerializationHelper: Nino.Serialization.NinoWrapperBase<AccountMerge>
        {
            #region NINO_CODEGEN
            public SerializationHelper()
            {
                int ret = 1;
                ret += sizeof(System.Int32);
                ret += sizeof(System.Int32);
                ret += sizeof(System.DateTime);
                Nino.Serialization.Serializer.SetFixedSize<Nino.Benchmark.Models.AccountMerge>(ret);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override void Serialize(AccountMerge value, ref Nino.Serialization.Writer writer)
            {
                if(value == null)
                {
                    writer.Write(false);
                    return;
                }
                writer.Write(true);
                writer.Write(value.OldAccountId);
                writer.Write(value.NewAccountId);
                writer.Write(value.MergeDate);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override AccountMerge Deserialize(Nino.Serialization.Reader reader)
            {
                if(!reader.ReadBool())
                    return null;
                AccountMerge value = new AccountMerge();
                value.OldAccountId = reader.Read<System.Int32>(sizeof(System.Int32));
                value.NewAccountId = reader.Read<System.Int32>(sizeof(System.Int32));
                value.MergeDate = reader.Read<System.DateTime>(sizeof(System.DateTime));
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetSize(AccountMerge value)
            {
                if(value == null)
                {
                    return 1;
                }
                return Nino.Serialization.Serializer.GetFixedSize<Nino.Benchmark.Models.AccountMerge>();
            }
            #endregion
        }
    }
}