namespace EOkulStudio.Core.Results;

public enum IssueCode
{
    // Çıktı üretilmez
    FileNotFound,
    CorruptFile,
    UnsupportedFormat,
    InsideOutputFolder,
    NoFace,
    MultipleFaces,
    OutputNotWritable,
    ProcessingError,

    // Çıktı üretilir, kontrol gerekir
    FaceTooSmall,
    LowResolution,
    Blurry,
    LowLight,
    ExcessiveTilt,
    EyesMaybeClosed,
    UncertainBackground,
    HeadCutInSource,
    ShouldersCut,
    HeadCut,
    HeadTight,
    ChinCut,
    FaceSmallInFrame,
    FaceLargeInFrame,
    OffCenter,
    EyeLevel,
    EyeTilt,
    FileSizeOutOfRange,
}
