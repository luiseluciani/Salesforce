WorkflowToTriggerUtility
==========

Went through a process of cleaning up Field Update workflows on an object that had too much custom logic (WF and triggers).

Moved all of the Workflow field updates into before update triggers, to alleviate the load on the system and add certainty to the process.

This tool translates the metadata of an object's workflow file into apex code.
