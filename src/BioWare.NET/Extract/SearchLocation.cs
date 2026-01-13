namespace BioWare.NET.Extract
{

    /// <summary>
    /// Enumeration representing different locations for searching resources in a KOTOR installation.
    /// </summary>
    public enum SearchLocation
    {
        /// <summary>
        /// Resources in the installation's override directory and any nested subfolders.
        /// </summary>
        OVERRIDE = 0,

        /// <summary>
        /// Encapsulated resources in the installation's 'modules' directory.
        /// </summary>
        MODULES = 1,

        /// <summary>
        /// Encapsulated resources linked to the installation's 'chitin.key' file.
        /// </summary>
        CHITIN = 2,

        /// <summary>
        /// Encapsulated resources in the installation's 'TexturePacks/swpc_tex_tpa.erf' file.
        /// </summary>
        TEXTURES_TPA = 3,

        /// <summary>
        /// Encapsulated resources in the installation's 'TexturePacks/swpc_tex_tpb.erf' file.
        /// </summary>
        TEXTURES_TPB = 4,

        /// <summary>
        /// Encapsulated resources in the installation's 'TexturePacks/swpc_tex_tpc.erf' file.
        /// </summary>
        TEXTURES_TPC = 5,

        /// <summary>
        /// Encapsulated resources in the installation's 'TexturePacks/swpc_tex_gui.erf' file.
        /// </summary>
        TEXTURES_GUI = 6,

        /// <summary>
        /// Resource files in the installation's 'StreamMusic' directory and any nested subfolders.
        /// </summary>
        MUSIC = 7,

        /// <summary>
        /// Resource files in the installation's 'StreamSounds' directory and any nested subfolders.
        /// </summary>
        SOUND = 8,

        /// <summary>
        /// Resource files in the installation's 'StreamVoice' or 'StreamWaves' directory and any nested subfolders.
        /// </summary>
        VOICE = 9,

        /// <summary>
        /// Encapsulated resources in the installation's 'lips' directory.
        /// </summary>
        LIPS = 10,

        /// <summary>
        /// Encapsulated resources in the installation's 'rims' directory.
        /// </summary>
        RIMS = 11,

        /// <summary>
        /// Encapsulated resources stored in the capsules specified in method parameters.
        /// </summary>
        CUSTOM_MODULES = 12,

        /// <summary>
        /// Resource files stored in the folders specified in the method parameters.
        /// </summary>
        CUSTOM_FOLDERS = 13
    }
}

