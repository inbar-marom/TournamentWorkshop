import requests
import zipfile
import os

zip_path = r"C:\Users\saplizki\Downloads\StrategicMind_Submission.zip"
team_name = "StrategicMind"

# Extract files from ZIP
files_data = []
with zipfile.ZipFile(zip_path, 'r') as zip_ref:
    for file_name in zip_ref.namelist():
        if file_name.endswith('/') or file_name.startswith('.'):
            continue
        with zip_ref.open(file_name) as file:
            content = file.read().decode('utf-8', errors='ignore')
            files_data.append({
                "FileName": os.path.basename(file_name),
                "Code": content
            })

print(f"Extracted {len(files_data)} files")

# Prepare request
payload = {
    "TeamName": team_name,
    "Files": files_data
}

# Call API
url = "http://localhost:8080/api/bots/verify"
print(f"Calling {url}...")
response = requests.post(url, json=payload, timeout=60)

print(f"\nStatus Code: {response.status_code}")
print(f"Response:")
print(response.json())
