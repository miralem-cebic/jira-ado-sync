# Choosing which Jira issues are synced: the "Sync to ADO" button

Not every Jira issue belongs in Azure DevOps. Some are worked on by people who never touch ADO. The sync therefore
creates the ADO work item only for issues that carry the label `sync-ado`.

People do not type the label. They click a button in the issue, and a Jira Automation rule adds the label. The label is
visible on the issue, so everyone can see that a sync was requested, by whom and when.

> **Status.** The button is configuration in Jira and works today. The sync itself does not yet check the label
> (roadmap, Phase 3: "Opt-in by label"). Until then every issue of a mapped project is a candidate. Set the button up
> now, so that the label is already on the issues when the check ships.

## Create the rule

You need Jira administrator rights, or project administrator rights for a rule that is limited to one project.

1. **Project settings > Automation > Create rule** (or **Settings > System > Global automation** for several projects).
2. **Trigger: Manual trigger.** Name it `Sync to ADO`. Under *Who can trigger this rule*, pick a project role or group
   (for example the developers). Everyone else does not see the button.
3. **Condition (optional): Issue fields condition.** *Labels* does not contain `sync-ado`. The button then does nothing
   twice. You can also limit the *Issue type* here.
4. **Action: Edit issue.** Choose *Labels*, then *Add* `sync-ado`.
5. **Action (optional): Add comment.** For example `Sync to ADO requested by {{initiator.displayName}}`.
6. Name the rule, then turn it on.

Users find the button in the issue under **Automation** (the lightning bolt).

## Rules to keep

- **Do not run the rule as the sync's service account.** The sync drops every event that its own identity caused
  (`jira.serviceAccountId`, DESIGN.md section 6). If the rule ran as that account, the label change would count as an
  echo and the sync would never see the request. Keep the default actor, *Automation for Jira*, or use a different
  account.
- **Do not let the sync add or remove the label.** The label is a request from a person.
- **Removing the label does not unlink an issue** (planned behaviour). It only controls whether a work item is created.
  An issue that already has a pair keeps syncing.

## Epics without labels

If your site hides the *Labels* field on epics, check three things:

- Whether the field is hidden in the *field configuration* of the epic issue type, or only missing from its *screens*.
  Automation can usually set a field that is not on a screen, but not one that the field configuration hides. Try the
  button on a test epic.
- If it is hidden in the field configuration, show the field for epics. That is the simplest fix.
- If epics must stay label-free, they need another trigger, for example a component or a custom field. Say so before
  the opt-in check is built, because the choice is part of the configuration.

## Narrow the webhook as well

The Jira webhook can carry a JQL filter, for example `project = PROJ AND labels = "sync-ado"`. Issues without the label
then never reach the Function, and their content does not leave Jira. Check in your site which fields the webhook
filter accepts. The webhook is registered once per project or globally (DESIGN.md section 11, open).

Without the label filter on the webhook, the receiver gets every issue and the sync has to decide on the label alone.
