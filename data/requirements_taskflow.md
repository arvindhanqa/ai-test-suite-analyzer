# TaskFlow - Project Management Tool - Requirements Document

**Version:** 1.0  
**Date:** January 31, 2026  
**Project:** Fictional Project Management App for Batch Processing Testing  
**Purpose:** Second dummy requirements file for AI Test Suite Analyzer batch testing

---

## 1. TASK MANAGEMENT

### 1.1 Create Task

#### Functional Requirements
- FR-TASK-001: System shall allow users to create a new task with a title, description, and due date
- FR-TASK-002: System shall allow users to assign a task to one or more team members
- FR-TASK-003: System shall allow users to set task priority (Low, Medium, High, Critical)
- FR-TASK-004: System shall allow users to add tags/labels to a task
- FR-TASK-005: System shall automatically set task status to "To Do" upon creation

#### Business Rules
- BR-TASK-001: Task title must be unique within a project
- BR-TASK-002: Due date cannot be set in the past
- BR-TASK-003: Maximum 5 tags per task
- BR-TASK-004: A task can only be assigned to members of the same project
- BR-TASK-005: Tasks created after 11:59 PM are logged under the next calendar day (UTC)

#### Validation Rules
- VR-TASK-001: Title must be 3-100 characters
- VR-TASK-002: Description must not exceed 2000 characters
- VR-TASK-003: Due date must be valid date format (YYYY-MM-DD)
- VR-TASK-004: Priority must be one of: Low, Medium, High, Critical
- VR-TASK-005: At least one assignee is required

#### Error Handling
- EH-TASK-001: If title already exists in project, display "A task with this title already exists in this project"
- EH-TASK-002: If due date is in the past, display "Due date cannot be in the past"
- EH-TASK-003: If assignee is not a project member, display "User [name] is not a member of this project"
- EH-TASK-004: If tag limit exceeded, display "Maximum 5 tags allowed per task"

---

### 1.2 Update Task

#### Functional Requirements
- FR-TASK-010: System shall allow task creator or assignee to update task details
- FR-TASK-011: System shall allow users to change task status (To Do, In Progress, Done, Cancelled)
- FR-TASK-012: System shall track all changes to a task in an activity log
- FR-TASK-013: System shall allow users to reassign a task to a different team member
- FR-TASK-014: System shall notify assignees when task details are updated

#### Business Rules
- BR-TASK-010: Only task creator or current assignee can update the task
- BR-TASK-011: A task marked "Done" cannot be moved back to "In Progress" without approval
- BR-TASK-012: Due date can only be extended, not shortened, by assignees (creators can change freely)
- BR-TASK-013: Status change to "Done" automatically records completion timestamp

#### Validation Rules
- VR-TASK-010: Updated title must still be unique within project
- VR-TASK-011: New due date must be valid and not in the past
- VR-TASK-012: Status must be a valid status value

#### Error Handling
- EH-TASK-010: If non-authorized user tries to update, display "You don't have permission to update this task"
- EH-TASK-011: If moving Done task back to In Progress without approval, display "Requires project manager approval"
- EH-TASK-012: If assignee tries to shorten due date, display "Only the task creator can shorten due dates"

---

### 1.3 Delete Task

#### Functional Requirements
- FR-TASK-020: System shall allow task creator or project admin to delete a task
- FR-TASK-021: System shall require confirmation before deleting a task
- FR-TASK-022: System shall notify all assignees when a task is deleted
- FR-TASK-023: System shall archive deleted tasks for 30 days before permanent removal

#### Business Rules
- BR-TASK-020: Tasks in "In Progress" status cannot be deleted directly (must be cancelled first)
- BR-TASK-021: Deleted tasks are recoverable within 30 days
- BR-TASK-022: Only project admin can permanently delete archived tasks

#### Validation Rules
- VR-TASK-020: User must be task creator or project admin
- VR-TASK-021: Task must exist and belong to user's project

#### Error Handling
- EH-TASK-020: If task is In Progress, display "Cancel the task first before deleting"
- EH-TASK-021: If user lacks permission, display "Only task creator or project admin can delete tasks"

---

## 2. PROJECT DASHBOARD

### 2.1 Dashboard Overview

#### Functional Requirements
- FR-DASH-001: System shall display summary of all tasks across the project
- FR-DASH-002: System shall show task count by status (To Do, In Progress, Done, Cancelled)
- FR-DASH-003: System shall display a progress bar showing overall project completion percentage
- FR-DASH-004: System shall show upcoming tasks sorted by due date (next 7 days)
- FR-DASH-005: System shall display recent activity feed (last 10 activities)

#### Business Rules
- BR-DASH-001: Progress percentage = (Done tasks / Total active tasks) * 100
- BR-DASH-002: Cancelled tasks are excluded from progress calculation
- BR-DASH-003: Dashboard data refreshes every 5 minutes automatically
- BR-DASH-004: Only project members can view the dashboard

#### Validation Rules
- VR-DASH-001: User must be an active member of the project
- VR-DASH-002: Project must have at least one task to display meaningful dashboard

#### Error Handling
- EH-DASH-001: If user is not a project member, display "Access denied. You are not a member of this project"
- EH-DASH-002: If project has no tasks, display "No tasks yet. Create your first task to get started!"

---

### 2.2 Filter and Sort Tasks

#### Functional Requirements
- FR-DASH-010: System shall allow filtering tasks by status
- FR-DASH-011: System shall allow filtering tasks by priority
- FR-DASH-012: System shall allow filtering tasks by assignee
- FR-DASH-013: System shall allow sorting tasks by due date, priority, or creation date
- FR-DASH-014: System shall allow combining multiple filters simultaneously

#### Business Rules
- BR-DASH-010: Filters are applied with AND logic when multiple selected
- BR-DASH-011: Filter selections persist during the session
- BR-DASH-012: Sort order defaults to due date ascending

#### Validation Rules
- VR-DASH-010: Filter values must be valid options
- VR-DASH-011: Assignee filter must show only current project members

#### Error Handling
- EH-DASH-010: If no tasks match filters, display "No tasks match your filters. Try adjusting your selection."

---

## 3. TEAM COLLABORATION

### 3.1 Comments

#### Functional Requirements
- FR-COLLAB-001: System shall allow project members to add comments to any task
- FR-COLLAB-002: System shall allow users to edit their own comments within 24 hours
- FR-COLLAB-003: System shall allow users to delete their own comments
- FR-COLLAB-004: System shall support @mentions to notify specific team members
- FR-COLLAB-005: System shall display comments in chronological order

#### Business Rules
- BR-COLLAB-001: Comments cannot be edited after 24 hours
- BR-COLLAB-002: Maximum 500 characters per comment
- BR-COLLAB-003: @mentions trigger email notification to mentioned user
- BR-COLLAB-004: Deleted comments show as "[Comment deleted]" placeholder

#### Validation Rules
- VR-COLLAB-001: Comment must be 1-500 characters
- VR-COLLAB-002: User must be a project member to comment
- VR-COLLAB-003: @mentioned user must be a project member

#### Error Handling
- EH-COLLAB-001: If comment exceeds 500 chars, display "Comment too long. Maximum 500 characters allowed"
- EH-COLLAB-002: If editing after 24 hours, display "Comments can only be edited within 24 hours"
- EH-COLLAB-003: If @mentioning non-member, display "User is not a member of this project"

---

### 3.2 Notifications

#### Functional Requirements
- FR-COLLAB-010: System shall send notifications for task assignments
- FR-COLLAB-011: System shall send notifications for task status changes
- FR-COLLAB-012: System shall send notifications for @mentions in comments
- FR-COLLAB-013: System shall allow users to configure notification preferences
- FR-COLLAB-014: System shall display unread notification count in header

#### Business Rules
- BR-COLLAB-010: Notifications are sent via email and in-app
- BR-COLLAB-011: Users can disable email notifications but in-app notifications always active
- BR-COLLAB-012: Notification preferences saved per project
- BR-COLLAB-013: Notifications older than 30 days are auto-deleted

#### Validation Rules
- VR-COLLAB-010: Notification preferences must be valid options (All, Mentions Only, None for email)

#### Error Handling
- EH-COLLAB-010: If email delivery fails, display in-app notification only with note "Email notification failed"

---

## APPENDIX A: Business Constants

| Constant | Value |
|----------|-------|
| Max Task Title Length | 100 characters |
| Max Description Length | 2000 characters |
| Max Tags Per Task | 5 |
| Comment Edit Window | 24 hours |
| Max Comment Length | 500 characters |
| Dashboard Refresh Interval | 5 minutes |
| Deleted Task Recovery Window | 30 days |
| Notification Auto-Delete | 30 days |
| Upcoming Tasks Window | 7 days |
| Recent Activity Feed | Last 10 items |

---

**End of Requirements Document**
