# Third-party notices

Material from other projects that ships inside YCR. NuGet packages carry their own licences and are not listed here.

## SecLists — common-password blocklist

- **Used in:** `src/YCR.Infrastructure/Identity/common-passwords.txt`, embedded in `YCR.Infrastructure` as the offline blocklist of the staff password policy (ADR-0023 item 2; F-002 plan P9).
- **Source:** SecLists, <https://github.com/danielmiessler/SecLists>, file `Passwords/Common-Credentials/xato-net-10-million-passwords-1000000.txt` at commit `7ee9b27880ef4a2400c4940956875721f025f76b` (downloaded 2026-09-23). The F-002 plan named `10-million-password-list-top-1000000.txt`; SecLists deleted that file at commit `4ff3ff8ef6b1c429db800e4a9b9deeac7425c5b6` as a duplicate of the `xato-net-10-million-passwords` lists, so the surviving copy was used.
- **Source SHA-256:** `424a3e03a17df0a2bc2b3ca749d81b04e79d59cb7aeec8876a5a3f308d0caf51` (1,000,000 lines).
- **Filter:** every line lower-cased with `ToLowerInvariant`; kept when it is 12 to 128 Unicode scalar values long (shorter passwords are refused by length anyway); de-duplicated; sorted ordinally; written as UTF-8 without BOM, one entry per line, `\n` line endings. Result: 46,146 entries. The .NET 10 file-based program used (`dotnet run filter-blocklist.cs -- <source> <output>`):

  ```csharp
  using System.Text;
  var kept = new SortedSet<string>(StringComparer.Ordinal);
  foreach (var line in File.ReadLines(args[0], Encoding.UTF8))
  {
      var entry = line.TrimEnd('\r').ToLowerInvariant();
      var length = entry.EnumerateRunes().Count();
      if (length is >= 12 and <= 128) kept.Add(entry);
  }
  File.WriteAllText(args[1], string.Concat(kept.Select(entry => entry + "\n")), new UTF8Encoding(false));
  ```

- **Embedded file SHA-256:** `e1e5d002f1b7144d4826ff33ac03fa25ce5e1bb214ffde9c82da2245190716fb` — asserted by `CommonPasswordBlocklistTests.Blocklist_Sha256MatchesNotice`. `.gitattributes` fixes the file's line endings to `\n` so this hash holds on every checkout.
- **Licence:** MIT.

  ```text
  MIT License

  Copyright (c) 2018 Daniel Miessler

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
  ```
