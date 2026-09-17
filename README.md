# RecordPulse

A fast, lightweight, and modern entity and contact management system built in C# and Windows Forms with an embedded SQLite backend.

---

## Features

* **Universal File Ingestion:** Drag and drop or import records directly from CSV, TSV, JSON, or XML files with automatic header mapping.
* **Ranked Relevance Search:** Fast, weighted querying across names, email addresses, phone numbers, job titles, and locations.
* **Modal Record Editor:** Dedicated pop-up dialog to view, add, or edit records with clean validation. Double-click any row in the table to edit.
* **Dynamic Schema Adaptation:** Switch between different `.db` files on the fly. Works with both standard records schemas and common contact structures.
* **Multi-Format Export:** Export your current datasets cleanly to CSV, TSV, JSON, or XML.
* **Direct Email Dispatch:** Click to compose via your default desktop mail client, with automatic fallback to webmail.
* **Modern Dark UI:** High-contrast dark theme designed for visual comfort and high-DPI scaling.

---

## Tech Stack

* **Language:** C# (.NET)
* **Framework:** Windows Forms (WinForms)
* **Database:** SQLite (`Microsoft.Data.Sqlite`)

---

## Getting Started

### Prerequisites
* Windows 10 / 11
* [.NET SDK](https://dotnet.microsoft.com/download) (Version 8.0 or later)

### Running from Source
1. Clone or download this repository.
2. Open PowerShell in the project root:
   ```powershell
   dotnet run
