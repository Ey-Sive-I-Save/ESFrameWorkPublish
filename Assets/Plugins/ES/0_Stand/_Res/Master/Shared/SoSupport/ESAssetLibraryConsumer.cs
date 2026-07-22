using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class ESAssetLibraryConsumer : LibConsumer<ESAssetLibrary>
    {
    }

    [System.Obsolete("Use ESAssetLibraryConsumer.")]
    public class ResLibConsumer : ESAssetLibraryConsumer
    {
    }
}
