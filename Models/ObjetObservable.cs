using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Chatp2pWPF.Models;

public class ObjetObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Maj([CallerMemberName] string? nom = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));

    protected void MajPlusieurs(params string[] noms)
    {
        foreach (var nom in noms) Maj(nom);
    }
}
