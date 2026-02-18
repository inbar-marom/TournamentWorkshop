"""
Bot Submission API MCP Server
FastMCP server for managing tournament bot submissions
"""

from fastmcp import FastMCP
from typing import Optional, List, Dict, Any
from datetime import datetime
from pydantic import BaseModel, Field
import requests
import os
import zipfile
import io

# Initialize FastMCP server
mcp = FastMCP("Bot Submission API")

# In-memory storage for demo purposes
# In production, this would connect to a real database
submissions_db: Dict[str, Dict[str, Any]] = {}


class BotSubmission(BaseModel):
    """Model for bot submission data"""
    bot_name: str = Field(..., description="Name of the bot")
    team_name: str = Field(..., description="Name of the team")
    bot_version: str = Field(..., description="Version of the bot")
    repository_url: str = Field(..., description="Git repository URL")
    description: Optional[str] = Field(None, description="Bot description")
    language: Optional[str] = Field(None, description="Programming language")
    framework: Optional[str] = Field(None, description="Framework or platform used")


# API Base URL for tournament resources
# TOURNAMENT_API_BASE = "http://4.210.232.147:8080"
TOURNAMENT_API_BASE = "http://localhost:8080"

# Bypass verification mode (for testing/development only)
_bypass_verification = False


@mcp.tool()
def download_template(
    template_name: str,
    output_directory: str = "."
) -> Dict[str, Any]:
    """
    Download a bot template from the tournament API and extract it.
    
    Args:
        template_name: Name of the template to download (e.g., 'TournamentWorkshopBotApi' or 'UserBot')
        output_directory: Directory where the template should be extracted (default: current directory)
    
    Returns:
        Dictionary containing download status and extracted files list
    """
    try:
        # Construct API URL
        url = f"{TOURNAMENT_API_BASE}/api/resources/templates/{template_name}"
        
        # Make GET request
        response = requests.get(url, timeout=30)
        
        if response.status_code == 200:
            # Create output directory if it doesn't exist
            os.makedirs(output_directory, exist_ok=True)
            
            # Extract ZIP content
            with zipfile.ZipFile(io.BytesIO(response.content)) as zip_ref:
                zip_ref.extractall(output_directory)
                extracted_files = zip_ref.namelist()
            
            return {
                "success": True,
                "message": f"Template '{template_name}' downloaded and extracted successfully",
                "output_directory": os.path.abspath(output_directory),
                "extracted_files": extracted_files
            }
        else:
            return {
                "success": False,
                "error": f"Failed to download template. Status code: {response.status_code}",
                "message": response.text
            }
            
    except requests.exceptions.RequestException as e:
        return {
            "success": False,
            "error": f"Network error while downloading template: {str(e)}"
        }
    except zipfile.BadZipFile:
        return {
            "success": False,
            "error": "Downloaded file is not a valid ZIP archive"
        }
    except Exception as e:
        return {
            "success": False,
            "error": f"Unexpected error: {str(e)}"
        }


@mcp.tool()
def verify_bot_submission(
    zip_file_path: str,
    team_name: str = "DefaultTeam"
) -> Dict[str, Any]:
    """
    Verify a bot submission by uploading it to the tournament API for validation.
    
    Args:
        zip_file_path: Path to the ZIP file containing bot submission files
        team_name: Name of the team submitting the bot
    
    Returns:
        Dictionary containing verification results including errors and warnings
    """
    try:
        # Check if the ZIP file exists
        if not os.path.exists(zip_file_path):
            return {
                "success": False,
                "error": f"File not found: {zip_file_path}"
            }
        
        # Extract files from ZIP
        files_data = []
        with zipfile.ZipFile(zip_file_path, 'r') as zip_ref:
            for file_name in zip_ref.namelist():
                # Skip directories and hidden files
                if file_name.endswith('/') or file_name.startswith('.'):
                    continue
                    
                with zip_ref.open(file_name) as file:
                    content = file.read().decode('utf-8', errors='ignore')
                    files_data.append({
                        "FileName": os.path.basename(file_name),
                        "Code": content
                    })
        
        if not files_data:
            return {
                "success": False,
                "error": "No valid files found in ZIP archive"
            }
        
        # Prepare request payload
        payload = {
            "TeamName": team_name,
            "Files": files_data
        }
        
        # Debug: Log file names being sent
        print(f"DEBUG - Sending {len(files_data)} files:")
        for i, f in enumerate(files_data[:5]):  # Show first 5
            print(f"  {i+1}. {f['FileName']} ({len(f['Code'])} chars)")
        
        # Call tournament API verify endpoint
        url = f"{TOURNAMENT_API_BASE}/api/bots/verify"
        response = requests.post(url, json=payload, timeout=60)
        
        if response.status_code == 200:
            result = response.json()
            # Debug: Log the actual response
            print(f"DEBUG - API Response: {result}")
            # API returns camelCase fields
            return {
                "success": result.get("success", result.get("isValid", False)),
                "is_valid": result.get("isValid", False),
                "message": result.get("message", "Verification completed"),
                "errors": result.get("errors", []),
                "warnings": result.get("warnings", []),
                "files_verified": len(files_data),
                "raw_response": result
            }
        else:
            return {
                "success": False,
                "error": f"Verification API returned status {response.status_code}",
                "details": response.text
            }
            
    except zipfile.BadZipFile:
        return {
            "success": False,
            "error": "Invalid ZIP file"
        }
    except requests.exceptions.RequestException as e:
        return {
            "success": False,
            "error": f"Network error: {str(e)}"
        }
    except Exception as e:
        return {
            "success": False,
            "error": f"Unexpected error: {str(e)}"
        }


@mcp.tool()
def submit_bot_to_tournament(
    zip_file_path: str,
    team_name: str,
    overwrite: bool = True
) -> Dict[str, Any]:
    """
    Submit a bot to the tournament by uploading it to the tournament API.
    
    Args:
        zip_file_path: Path to the ZIP file containing bot submission files
        team_name: Name of the team submitting the bot
        overwrite: Whether to overwrite existing submission (default: True)
    
    Returns:
        Dictionary containing submission results
    """
    try:
        # Check if the ZIP file exists
        if not os.path.exists(zip_file_path):
            return {
                "success": False,
                "error": f"File not found: {zip_file_path}"
            }
        
        # Extract files from ZIP
        files_data = []
        with zipfile.ZipFile(zip_file_path, 'r') as zip_ref:
            for file_name in zip_ref.namelist():
                # Skip directories and hidden files
                if file_name.endswith('/') or file_name.startswith('.'):
                    continue
                    
                with zip_ref.open(file_name) as file:
                    content = file.read().decode('utf-8', errors='ignore')
                    files_data.append({
                        "FileName": os.path.basename(file_name),
                        "Code": content
                    })
        
        if not files_data:
            return {
                "success": False,
                "error": "No valid files found in ZIP archive"
            }
        
        # Prepare request payload
        payload = {
            "TeamName": team_name,
            "Files": files_data,
            "Overwrite": overwrite
        }
        
        # Call tournament API submit endpoint
        url = f"{TOURNAMENT_API_BASE}/api/bots/submit"
        response = requests.post(url, json=payload, timeout=60)
        
        if response.status_code == 200:
            result = response.json()
            return {
                "success": result.get("Success", False),
                "team_name": result.get("TeamName"),
                "submission_id": result.get("SubmissionId"),
                "message": result.get("Message", ""),
                "errors": result.get("Errors", []),
                "files_submitted": len(files_data)
            }
        else:
            return {
                "success": False,
                "error": f"Submission API returned status {response.status_code}",
                "details": response.text
            }
            
    except zipfile.BadZipFile:
        return {
            "success": False,
            "error": "Invalid ZIP file"
        }
    except requests.exceptions.RequestException as e:
        return {
            "success": False,
            "error": f"Network error: {str(e)}"
        }
    except Exception as e:
        return {
            "success": False,
            "error": f"Unexpected error: {str(e)}"
        }


@mcp.tool()
def submit_bot(
    bot_name: str,
    team_name: str,
    bot_version: str,
    repository_url: str,
    description: str = "",
    language: str = "",
    framework: str = ""
) -> Dict[str, Any]:
    """
    Submit a new bot entry to the tournament.
    
    Args:
        bot_name: Name of the bot
        team_name: Name of the team submitting the bot
        bot_version: Version of the bot (e.g., 1.0.0)
        repository_url: URL to the bot's source code repository
        description: Optional description of the bot's strategy or features
        language: Programming language used (e.g., Python, JavaScript, Java)
        framework: Framework or platform used (e.g., TensorFlow, PyTorch)
    
    Returns:
        Dictionary containing submission ID and confirmation details
    """
    submission_id = f"sub_{len(submissions_db) + 1}_{datetime.now().strftime('%Y%m%d%H%M%S')}"
    
    submission = {
        "submission_id": submission_id,
        "bot_name": bot_name,
        "team_name": team_name,
        "bot_version": bot_version,
        "repository_url": repository_url,
        "description": description,
        "language": language,
        "framework": framework,
        "status": "pending",
        "submitted_at": datetime.now().isoformat(),
        "updated_at": datetime.now().isoformat()
    }
    
    submissions_db[submission_id] = submission
    
    return {
        "success": True,
        "submission_id": submission_id,
        "message": f"Bot '{bot_name}' successfully submitted by team '{team_name}'",
        "submission": submission
    }


@mcp.tool()
def get_submission(submission_id: str) -> Dict[str, Any]:
    """
    Retrieve details of a specific bot submission.
    
    Args:
        submission_id: Unique identifier of the submission
    
    Returns:
        Dictionary containing submission details
    """
    if submission_id not in submissions_db:
        return {
            "success": False,
            "error": f"Submission '{submission_id}' not found"
        }
    
    return {
        "success": True,
        "submission": submissions_db[submission_id]
    }


@mcp.tool()
def list_submissions(
    team_name: Optional[str] = None,
    status: Optional[str] = None,
    limit: int = 10
) -> Dict[str, Any]:
    """
    List bot submissions with optional filtering.
    
    Args:
        team_name: Filter by team name (optional)
        status: Filter by status (pending, approved, rejected, testing) (optional)
        limit: Maximum number of submissions to return (default: 10)
    
    Returns:
        Dictionary containing list of submissions
    """
    submissions = list(submissions_db.values())
    
    # Apply filters
    if team_name:
        submissions = [s for s in submissions if s["team_name"].lower() == team_name.lower()]
    
    if status:
        submissions = [s for s in submissions if s["status"].lower() == status.lower()]
    
    # Apply limit
    submissions = submissions[:limit]
    
    return {
        "success": True,
        "count": len(submissions),
        "submissions": submissions
    }


@mcp.tool()
def update_submission(
    submission_id: str,
    bot_version: Optional[str] = None,
    description: Optional[str] = None,
    repository_url: Optional[str] = None,
    status: Optional[str] = None
) -> Dict[str, Any]:
    """
    Update an existing bot submission.
    
    Args:
        submission_id: Unique identifier of the submission to update
        bot_version: New version number (optional)
        description: Updated description (optional)
        repository_url: Updated repository URL (optional)
        status: Updated status (pending, approved, rejected, testing) (optional)
    
    Returns:
        Dictionary containing updated submission details
    """
    if submission_id not in submissions_db:
        return {
            "success": False,
            "error": f"Submission '{submission_id}' not found"
        }
    
    submission = submissions_db[submission_id]
    
    # Update fields if provided
    if bot_version is not None:
        submission["bot_version"] = bot_version
    if description is not None:
        submission["description"] = description
    if repository_url is not None:
        submission["repository_url"] = repository_url
    if status is not None:
        submission["status"] = status
    
    submission["updated_at"] = datetime.now().isoformat()
    
    return {
        "success": True,
        "message": f"Submission '{submission_id}' updated successfully",
        "submission": submission
    }


@mcp.tool()
def delete_submission(submission_id: str) -> Dict[str, Any]:
    """
    Delete a bot submission.
    
    Args:
        submission_id: Unique identifier of the submission to delete
    
    Returns:
        Dictionary containing deletion confirmation
    """
    if submission_id not in submissions_db:
        return {
            "success": False,
            "error": f"Submission '{submission_id}' not found"
        }
    
    deleted_submission = submissions_db.pop(submission_id)
    
    return {
        "success": True,
        "message": f"Submission '{submission_id}' deleted successfully",
        "deleted_submission": deleted_submission
    }


@mcp.tool()
def get_submission_status(submission_id: str) -> Dict[str, Any]:
    """
    Check the current status of a bot submission.
    
    Args:
        submission_id: Unique identifier of the submission
    
    Returns:
        Dictionary containing submission status information
    """
    if submission_id not in submissions_db:
        return {
            "success": False,
            "error": f"Submission '{submission_id}' not found"
        }
    
    submission = submissions_db[submission_id]
    
    return {
        "success": True,
        "submission_id": submission_id,
        "bot_name": submission["bot_name"],
        "team_name": submission["team_name"],
        "status": submission["status"],
        "submitted_at": submission["submitted_at"],
        "updated_at": submission["updated_at"]
    }


@mcp.tool()
def get_statistics() -> Dict[str, Any]:
    """
    Get overall statistics about bot submissions.
    
    Returns:
        Dictionary containing submission statistics
    """
    total = len(submissions_db)
    
    status_counts = {}
    team_counts = {}
    language_counts = {}
    
    for submission in submissions_db.values():
        # Count by status
        status = submission["status"]
        status_counts[status] = status_counts.get(status, 0) + 1
        
        # Count by team
        team = submission["team_name"]
        team_counts[team] = team_counts.get(team, 0) + 1
        
        # Count by language
        language = submission.get("language", "unknown")
        if language:
            language_counts[language] = language_counts.get(language, 0) + 1
    
    return {
        "success": True,
        "total_submissions": total,
        "by_status": status_counts,
        "by_team": team_counts,
        "by_language": language_counts
    }


if __name__ == "__main__":
    # Run the server
    mcp.run()
