using System;
using System.ComponentModel.Composition;
using System.IO;
using TraderTools.Basics;

namespace TraderTools.Core.Services
{
    [Export(typeof(IDataDirectoryService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DataDirectoryService : IDataDirectoryService

    {
    private static string _mainDirectory;
    private string _applicationName = null;
    private string _mainDirectoryWithApplicationName;

    public DataDirectoryService()
    {
        _mainDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"TraderTools");
    }

    public static string GetMainDirectoryWithApplicationName(string applicationName)
    {
        return Path.Combine(_mainDirectory, applicationName);
    }

    public string MainDirectory => _mainDirectory;

    public string MainDirectoryWithApplicationName => _mainDirectoryWithApplicationName;

    public void SetApplicationName(string applicationName)
    {
        _applicationName = applicationName;

        if (!string.IsNullOrEmpty(_applicationName))
        {
            _mainDirectoryWithApplicationName = Path.Combine(_mainDirectory, _applicationName);
        }
        else
        {
            _mainDirectoryWithApplicationName = _mainDirectory;
        }
    }
    }
}