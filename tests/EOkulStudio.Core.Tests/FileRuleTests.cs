using System.Globalization;
using EOkulStudio.Core.Files;

namespace EOkulStudio.Core.Tests;

public class SupportedFilesTests
{
    [Theory]
    [InlineData("a.jpg")]
    [InlineData("a.JPG")]
    [InlineData("b.jpeg")]
    [InlineData("c.png")]
    [InlineData("d.bmp")]
    [InlineData("e.tif")]
    [InlineData("f.TIFF")]
    [InlineData("g.webp")]
    [InlineData("İlk Öğrenci 1.Jpg")]
    public void Photo_extensions_are_accepted(string name) =>
        SupportedFiles.Classify(Path.Combine("klasör", name)).Should().Be(FileKind.Photo);

    [Theory]
    [InlineData("a.heic")]
    [InlineData("a.HEIF")]
    [InlineData("a.avif")]
    [InlineData("a.gif")]
    [InlineData("a.jfif")]
    [InlineData("a.cr2")]
    [InlineData("a.dng")]
    public void Unsupported_images_are_reported(string name) =>
        SupportedFiles.Classify(name).Should().Be(FileKind.UnsupportedImage);

    [Theory]
    [InlineData("a.txt")]
    [InlineData("a.docx")]
    [InlineData("uzantisiz")]
    [InlineData(".gizli.jpg")]
    [InlineData("~$a.jpg")]
    [InlineData("Thumbs.db")]
    [InlineData("desktop.ini")]
    [InlineData("")]
    public void Other_files_are_ignored(string name) =>
        SupportedFiles.Classify(name).Should().Be(FileKind.Ignored);

    [Fact]
    public void Classification_does_not_depend_on_turkish_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            SupportedFiles.Classify("A.TIF").Should().Be(FileKind.Photo);
            SupportedFiles.Classify("A.TIFF").Should().Be(FileKind.Photo);
            SupportedFiles.Classify("DESKTOP.INI").Should().Be(FileKind.Ignored);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

public class OutputLocationTests
{
    [Fact]
    public void Output_goes_into_E_Okul_next_to_the_source() =>
        OutputLocation.DirectoryFor(Path.Combine("okul", "9A", "a.jpg"))
            .Should().Be(Path.Combine("okul", "9A", "E_Okul"));

    [Fact]
    public void Source_without_folder_is_rejected()
    {
        var act = () => OutputLocation.DirectoryFor("a.jpg");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("x/E_Okul", true)]
    [InlineData("x/e_okul/y", true)]
    [InlineData("E_OKUL", true)]
    [InlineData("x/E_Okul2", false)]
    [InlineData("x/E_Okul_eski/y", false)]
    [InlineData("x/y", false)]
    public void Output_tree_is_detected_by_whole_folder_name(string path, bool expected) =>
        OutputLocation.IsInOutputTree(path).Should().Be(expected);

    [Fact]
    public void File_name_alone_does_not_make_a_file_an_output()
    {
        OutputLocation.IsFileInOutputTree(Path.Combine("x", "E_Okul.jpg")).Should().BeFalse();
        OutputLocation.IsFileInOutputTree(Path.Combine("x", "E_Okul", "a.jpg")).Should().BeTrue();
    }
}

public class OutputNamingTests
{
    [Fact]
    public void Output_name_is_source_name_with_jpg_extension()
    {
        var names = OutputNaming.Assign(["İlk Öğrenci.png", "ali.veli.jpeg"]);

        names["İlk Öğrenci.png"].Should().Be("İlk Öğrenci.jpg");
        names["ali.veli.jpeg"].Should().Be("ali.veli.jpg");
    }

    [Theory]
    [InlineData("a.png", "a.jpg")]
    [InlineData("a.jpg", "a.png")]
    public void Collisions_get_a_suffix_regardless_of_input_order(string first, string second)
    {
        var names = OutputNaming.Assign([first, second]);

        names["a.jpg"].Should().Be("a.jpg");
        names["a.png"].Should().Be("a_2.jpg");
    }

    [Fact]
    public void Collisions_ignore_case()
    {
        var names = OutputNaming.Assign(["A.jpg", "a.png"]);

        names["A.jpg"].Should().Be("A.jpg");
        names["a.png"].Should().Be("a_2.jpg");
    }

    [Fact]
    public void Suffix_does_not_take_another_files_own_name()
    {
        var names = OutputNaming.Assign(["a.png", "a_2.png", "a.jpg"]);

        names["a.jpg"].Should().Be("a.jpg");
        names["a_2.png"].Should().Be("a_2.jpg");
        names["a.png"].Should().Be("a_3.jpg");
    }

    [Fact]
    public void Recorded_names_are_kept()
    {
        var recorded = new Dictionary<string, string> { ["a.png"] = "a.jpg" };

        var names = OutputNaming.Assign(["a.jpg", "a.png"], recorded);

        names["a.png"].Should().Be("a.jpg");
        names["a.jpg"].Should().Be("a_2.jpg");
    }

    [Fact]
    public void Output_of_a_removed_source_is_not_reused()
    {
        var recorded = new Dictionary<string, string> { ["a.jpg"] = "a.jpg" };

        var names = OutputNaming.Assign(["a.png"], recorded);

        names.Should().ContainSingle();
        names["a.png"].Should().Be("a_2.jpg");
    }

    [Fact]
    public void Repeated_source_is_listed_once() =>
        OutputNaming.Assign(["a.jpg", "A.JPG"]).Should().ContainSingle();

    [Fact]
    public void Long_names_are_shortened_to_the_windows_limit()
    {
        var stem = new string('a', 300);

        var name = OutputNaming.Assign([stem + ".png"])[stem + ".png"];

        name.Length.Should().Be(OutputNaming.MaxNameLength);
        name.Should().EndWith(".jpg");
    }

    [Fact]
    public void Suffixed_long_names_stay_within_the_limit()
    {
        var stem = new string('a', 251);

        var names = OutputNaming.Assign([stem + ".jpg", stem + ".png"]);

        names[stem + ".png"].Length.Should().Be(OutputNaming.MaxNameLength);
        names[stem + ".png"].Should().EndWith("_2.jpg");
    }

    [Fact]
    public void Shortening_does_not_split_a_surrogate_pair()
    {
        var source = new string('a', 250) + "😀.png";

        var name = OutputNaming.Assign([source])[source];

        name.Should().Be(new string('a', 250) + ".jpg");
    }

    [Fact]
    public void Next_free_name_skips_taken_ones() =>
        OutputNaming.NextFree("a.jpg", name => name is "a_2.jpg" or "a_3.jpg").Should().Be("a_4.jpg");
}
