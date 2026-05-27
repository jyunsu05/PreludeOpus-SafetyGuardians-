# Team Game Development & AI Coding Rules

You are an AI assistant collaborating on a Unity/Unreal game development team project. To prevent merge conflicts and ensure safe collaboration, you must strictly follow these workspace boundaries and rules.

## 🚨 CRITICAL RULE: Directory Ownership & Boundaries
1. **Identify the Active Developer**: 
   - Look at the path of the currently open/active file or the files being edited.
   - Identify the developer's folder under `Assets/` (e.g., `Assets/Kim`, `Assets/Lee`, `Assets/Park`). This is the "Active Developer Folder".

2. **No-Go Zone (Strict Isolation)**:
   - You are **ONLY** allowed to create, modify, or delete files inside the "Active Developer Folder" (e.g., `Assets/Kim/...`).
   - You must **NEVER** modify, overwrite, or delete files in other developers' folders (e.g., `Assets/Lee`, `Assets/Park`) without explicit permission.

3. **Exception Handling (Inter-folder Dependencies)**:
   - If you absolutely must reference or modify a script, prefab, or asset in another developer's folder:
     1. **STOP** writing code immediately.
     2. **REPORT** the necessity to the user in detail. Explain exactly *which* file outside your folder needs to be touched and *why*.
     3. **ASK FOR CONFIRMATION** before applying any changes. Do not use auto-apply/write features on files outside the active developer's folder.

## 🛠️ Unity-Specific Safety Guidelines
- **Unity Meta Files**: Do not manually modify or corrupt `.meta` files. If you generate a new script or prefab, ensure Unity's asset serialization rules are respected.
- **Component Referencing**: Prefer referencing other developers' systems via Interfaces or Manager classes rather than direct coupling to their scripts in other folders.
- **Prefabs**: Create and modify prefabs ONLY inside the Active Developer's folder. Do not override base prefabs located in other team members' directories.

---
Please acknowledge these rules before starting any task. If you are unsure which folder belongs to the current user, ask the user first: "Which developer folder should I work in?"