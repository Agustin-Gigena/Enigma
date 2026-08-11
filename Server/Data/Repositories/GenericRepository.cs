namespace Enigma.Server.Data.Repositories;

public abstract class GenericRepository<T> where T : class
{
    protected readonly EnigmaDbContext Context;
    public GenericRepository(EnigmaDbContext context)
    {
        Context = context;
    }

    public virtual T? GetById(int id, bool borradoLogico = false)
    {
        var entity = Context.Set<T>().Find(id);
        if (entity == null || (entity is GenericEntity genEntity && genEntity.BorradoLogico && !borradoLogico))
        {
            return null;
        }
        return entity;
    }

    public bool SetBorradoLogico(int id, bool borradoLogico)
    {
        var entity = Context.Set<T>().Find(id);
        if (entity == null)
        {
            return false;
        }

        if (entity is GenericEntity genEntity)
        {
            genEntity.BorradoLogico = borradoLogico;
            Context.SaveChanges();
            return true;
        }

        return false;
    }
}