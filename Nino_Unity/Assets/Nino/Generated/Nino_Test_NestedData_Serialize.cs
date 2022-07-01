/* this is generated by nino */
namespace Nino.Test
{
    public partial class NestedData
    {
        public static NestedData.SerializationHelper NinoSerializationHelper = new NestedData.SerializationHelper();
        public class SerializationHelper: Nino.Serialization.ISerializationHelper<NestedData>
        {
            #region NINO_CODEGEN
            public void NinoWriteMembers(NestedData value, Nino.Serialization.Writer writer)
            {
                writer.Write(value.name);
                if(value.ps != null)
                {
                    writer.CompressAndWrite(value.ps.Length);
                    foreach (var entry in value.ps)
                    {
                        Nino.Test.Data.NinoSerializationHelper.NinoWriteMembers(entry, writer);
                    }
                }
                else
                {
                    writer.CompressAndWrite(0);
                }
            }

            public void NinoWriteMembers(object val, Nino.Serialization.Writer writer)
            {
	            NinoWriteMembers((NestedData)val, writer);
            }

            public NestedData NinoReadMembers(Nino.Serialization.Reader reader)
            {
                NestedData value = new NestedData();
                value.name = reader.ReadString();
                value.ps = new Nino.Test.Data[reader.ReadLength()];
                for(int i = 0, cnt = value.ps.Length; i < cnt; i++)
                {
                    var value_ps_i = Nino.Test.Data.NinoSerializationHelper.NinoReadMembers(reader);
                    value.ps[i] = value_ps_i;
                }
                return value;
            }

            object Nino.Serialization.ISerializationHelper.NinoReadMembers(Nino.Serialization.Reader reader)
            {
	            return NinoReadMembers(reader);
            }
            #endregion
        }
    }
}