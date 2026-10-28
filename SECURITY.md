# Security

Boatdrill runs as the signed-in user, reads the folders you name and the backups you point it at, and
writes only to its own data folder (`%LOCALAPPDATA%\Boatdrill`) and the scratch folder inside it. It has
no network code, no listener and no update mechanism. A drill never writes into a destination.

If you find something that undermines any of that, please report it privately rather than in a public
issue: use GitHub's **Report a vulnerability** button on the repository's Security tab. You will get an
acknowledgement within a few days and a fix or a clear answer as soon as one exists.

Things that are not security issues: a verdict you disagree with (open a bug), or the "Unknown
publisher" warning on an unsigned build.
