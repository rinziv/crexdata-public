
using System.Globalization;
using System.Threading;
using UnityEngine;

public class CultureInitializer : MonoBehaviour
{
    void Awake()
    {
        CultureInfo culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ".";
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
