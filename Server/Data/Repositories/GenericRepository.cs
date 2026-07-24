namespace Enigma.Server.Data.Repositories;

public abstract class GenericRepository<T> where T : class
{
    private readonly EnigmaDbContext _context;
    public GenericRepository(EnigmaDbContext context)
    {
        _context = context;
    }

    public virtual T? GetById(int id, bool borradoLogico = false)
    {
        var entity = _context.Set<T>().Find(id);
        if (entity == null || (entity is GenericEntity genEntity && genEntity.BorradoLogico && !borradoLogico))
        {
            return null;
        }
        return entity;
    }

    public bool SetBorradoLogico(int id, bool borradoLogico)
    {
        var entity = _context.Set<T>().Find(id);
        if (entity == null)
        {
            return false;
        }

        if (entity is GenericEntity genEntity)
        {
            genEntity.BorradoLogico = borradoLogico;
            _context.SaveChanges();
            return true;
        }

        return false;
    }
}