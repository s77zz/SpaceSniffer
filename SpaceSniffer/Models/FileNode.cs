using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpaceSniffer.Models;

public class FileNode : ObservableObject
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _fullPath = "";
    public string FullPath
    {
        get => _fullPath;
        set => SetProperty(ref _fullPath, value);
    }

    private long _size;
    public long Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    private FileNodeType _type;
    public FileNodeType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    private double _sizeRatio;
    public double SizeRatio
    {
        get => _sizeRatio;
        set => SetProperty(ref _sizeRatio, value);
    }

    public ObservableCollection<FileNode> Children { get; } = new();
}
