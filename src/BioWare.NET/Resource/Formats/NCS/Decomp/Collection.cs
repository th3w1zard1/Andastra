namespace BioWare.NET.Resource.Formats.NCS.Decomp
{
    public abstract class Collection
    {
        public abstract IEnumerator<object> Iterator();
        public abstract bool AddAll(Collection c);
        public abstract bool AddAll(int index, Collection c);
    }
}





