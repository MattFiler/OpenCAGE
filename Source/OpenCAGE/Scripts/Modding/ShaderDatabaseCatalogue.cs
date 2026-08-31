using CATHODE;
using CATHODE.ShaderTypes;
using CathodeLib.Ubershaders;
using System.Collections.Generic;

namespace OpenCAGE.Modding
{
    /// <summary>
    /// Presents the harvested ShaderDatabase as the middle tier of CathodeLib's permutation
    /// service. The database itself stays here: it needs a pristine install to read from and the
    /// vanilla hash manifest to know which files still count as pristine, neither of which is
    /// CathodeLib's business. Instancing leaves the tier unset and resolves from the level pool
    /// and the reconstructed masters alone.
    /// </summary>
    public class ShaderDatabaseCatalogue : IUbershaderCatalogue
    {
        private readonly ShaderDatabase _database;
        private readonly string _gameRoot;

        public ShaderDatabaseCatalogue(ShaderDatabase database, string gameRoot)
        {
            _database = database;
            _gameRoot = gameRoot;
        }

        /// <summary>Registers this as the service's catalogue source. Call once at startup.</summary>
        public static void Register()
        {
            ShaderPermutationService.CatalogueProvider = gameRoot =>
                string.IsNullOrEmpty(gameRoot) || !ShaderDatabase.IsBuilt(gameRoot)
                    ? null : new ShaderDatabaseCatalogue(new ShaderDatabase(gameRoot), gameRoot);
        }

        public HashSet<long> FamilyMasks(SHADER_LIST family)
        {
            return _database.FamilyMasks(family);
        }

        public bool TryGet(SHADER_LIST family, long mask, out Shaders.Shader shader)
        {
            ShaderDatabase.Entry entry;
            if (!_database.TryGet(family, mask, out entry))
            {
                shader = null;
                return false;
            }
            shader = entry.ToShader(family);
            return true;
        }

        public IEnumerable<KeyValuePair<SHADER_LIST, int>> Families()
        {
            return ShaderDatabase.HarvestedFamilies(_gameRoot);
        }

        public IEnumerable<KeyValuePair<long, Shaders.Shader>> Entries(SHADER_LIST family)
        {
            foreach (ShaderDatabase.Entry entry in _database.FamilyEntries(family))
                yield return new KeyValuePair<long, Shaders.Shader>(entry.Mask, entry.ToShader(family));
        }
    }
}
